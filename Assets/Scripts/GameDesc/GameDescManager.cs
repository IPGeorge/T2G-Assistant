using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using T2G;
using System.Linq;

namespace T2G.Assistant
{
    /// <summary>
    /// Manages GameDesc design snapshots: create, save, load, list, and edit.
    /// Snapshot is the domain model (design-state), not runtime state.
    /// </summary>
    public sealed partial class GameDescManager
    {
        // -------------------------
        // Singleton
        // -------------------------
        private static readonly Lazy<GameDescManager> _instance = new Lazy<GameDescManager>(() => new GameDescManager());

        public static GameDescManager Instance => _instance.Value;

        private GameDescManager()
        {
            _saveGameDescFolder = Path.Combine(Application.persistentDataPath, "GameDescs");
            if (!Directory.Exists(_saveGameDescFolder))
            {
                Directory.CreateDirectory(_saveGameDescFolder);
            }

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Parent pointers not persisted
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        // -------------------------
        // Snapshot + Context
        // -------------------------
        /// <summary>
        /// Active design snapshot.
        /// </summary>
        public GameDesc Snapshot { get; private set; }

        // -------------------------
        // Internal
        // -------------------------
        private readonly string _saveGameDescFolder;
        private readonly JsonSerializerSettings _jsonSettings;
        public string CurrentSpaceName 
        { 
            get
            {
                return Assistant.Instance.GameProject.CurrentSpace;
            }
            set
            {
                Assistant.Instance.GameProject.CurrentSpace = value;
            }
        }
        public string CurrentProjectName 
        { 
            get
            {
                return Snapshot.ProjectName;
            }
        }

        // ============================================================
        // Project lifecycle
        // ============================================================

        public void CreateGameDescProject(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("projectName is empty.");

            CurrentSpaceName = null;
            CreateGameDesc(projectName);
            SaveGameDesc(projectName);
        }

        public void OpenOrCreateGameDesc(string projectName, string title)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("projectName is empty.");

            string filePath = Path.Combine(_saveGameDescFolder, projectName + ".json");

            if (File.Exists(filePath))
            {
                LoadGameDesc(filePath);
                Snapshot.ProjectName = projectName;
                Snapshot.Title = title ?? Snapshot.Title;
            }
            else
            {
                CreateGameDesc(projectName, title);
                SaveGameDesc(projectName);
            }
        }

        public void RecordInstruction(T2G.Instruction instruction, T2G.Response response)
        {
            if (instruction == null || Snapshot == null)
                return;

            Snapshot.InstructionHistory ??= new List<InstructionRecord>();

            var record = new InstructionRecord
            {
                InstructionJson = JsonConvert.SerializeObject(instruction, _jsonSettings),
                ExecutedUtc = DateTime.UtcNow,
                Succeeded = response?.Succeeded ?? false
            };

            Snapshot.InstructionHistory.Add(record);

            if (response?.Succeeded == true)
            {
                UpdateFromInstruction(instruction);
            }

            SaveGameDesc();
        }

        private void UpdateFromInstruction(T2G.Instruction instruction)
        {
            Debug.Log($"Update game after instruction {instruction.action} execution.");

            if (instruction == null || string.IsNullOrWhiteSpace(instruction.action))
                return;

            string action = instruction.action;

            if (action == T2G.Actions.create_space)
            {
                string spaceName = instruction.parameters.GetString("spaceName");
                if (!string.IsNullOrWhiteSpace(spaceName))
                {
                    AddSpace(spaceName);
                    CurrentSpaceName = spaceName;
                }
            }
            else if (action == T2G.Actions.create_object)
            {
                string objectName = instruction.parameters.GetString("Name");
                string desc = instruction.desc;
                Debug.Log($"[GameDescManager] create_object: objectName={objectName}, desc={desc}, CurrentSpaceName={CurrentSpaceName}");
                
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    // Auto-create space if it doesn't exist in GameDesc
                    if (FindSpace(CurrentSpaceName) == null)
                    {
                        AddSpace(CurrentSpaceName);
                    }
                    
                    // Add object to the space
                    try
                    {
                        string parentName = instruction.parameters.GetString("Parent");
                        AddObject(CurrentSpaceName, objectName, instruction.desc, parentName);
                        Debug.Log($"[GameDescManager] Created object: {objectName} with desc: {instruction?.desc}");

                        // Verify object was added
                        var space = FindSpace(CurrentSpaceName);
                        var obj = FindObjectInSpace(space, objectName);
                        Debug.Log($"[GameDescManager] Object added: {objectName}, space.Objects count: {space?.Objects?.Count ?? 0}");

                        // Populate Assets from instruction.assets
                        var instrAssets = instruction.assets;
                        if (instrAssets != null && instrAssets.Count > 0)
                        {
                            var spaceName = FindSpace(CurrentSpaceName);
                            var gameobj = FindObjectInSpace(spaceName, objectName);
                            if (gameobj != null)
                            {
                                gameobj.Assets ??= new List<string>();
                                foreach (var path in instrAssets)
                                {
                                    if (!gameobj.Assets.Contains(path))
                                        gameobj.Assets.Add(path);
                                }
                            }
                        }

                        // Populate position from instruction.parameters
                        string positionStr = instruction.parameters.GetString("position");
                        if (!string.IsNullOrWhiteSpace(positionStr))
                        {
                            var spaceName = FindSpace(CurrentSpaceName);
                            var gameObj = FindObjectInSpace(spaceName, objectName);
                            if (gameObj != null)
                            {
                                gameObj.Properties ??= new List<ValuePair>();
                                var posProp = gameObj.Properties.Find(p => string.Equals(p.name, "position", StringComparison.OrdinalIgnoreCase));
                                if (posProp != null) posProp.value = positionStr;
                                else gameObj.Properties.Add(new ValuePair("position", positionStr));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GameDescManager] Failed to create object: {ex.Message}\n{ex.StackTrace}");
                        AddObject(CurrentSpaceName, objectName, instruction.desc, null);

                        // Populate Assets from instruction.assets
                        var instrAssets = instruction.assets;
                        if (instrAssets != null && instrAssets.Count > 0)
                        {
                            var space = FindSpace(CurrentSpaceName);
                            var obj = FindObjectInSpace(space, objectName);
                            if (obj != null)
                            {
                                obj.Assets ??= new List<string>();
                                foreach (var path in instrAssets)
                                {
                                    if (!obj.Assets.Contains(path))
                                        obj.Assets.Add(path);
                                }
                            }
                        }

                        // Populate position from instruction.parameters
                        string positionStr = instruction.parameters.GetString("position");
                        if (!string.IsNullOrWhiteSpace(positionStr))
                        {
                            var space = FindSpace(CurrentSpaceName);
                            var obj = FindObjectInSpace(space, objectName);
                            if (obj != null)
                            {
                                obj.Properties ??= new List<ValuePair>();
                                var posProp = obj.Properties.Find(p => string.Equals(p.name, "position", StringComparison.OrdinalIgnoreCase));
                                if (posProp != null) 
                                    posProp.value = positionStr;
                                else 
                                    obj.Properties.Add(new ValuePair("position", positionStr));
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[GameDescManager] create_object skipped - objectName or CurrentSpaceName is empty");
                }
            }
            else if (action == T2G.Actions.add_component)
            {
                string objectName = instruction.parameters.GetString("objName");
                string componentType = instruction.parameters.GetString("type");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try
                    {
                        AddComponent(CurrentSpaceName, objectName, componentType, instruction);
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.set_property)
            {
                string objectName = instruction.parameters.GetString("Name");
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = instruction.parameters.GetString("objName");
                
                string propertyName = instruction.parameters.GetString("Property");
                if (string.IsNullOrWhiteSpace(propertyName))
                    propertyName = instruction.parameters.GetString("property");
                
                string valueStr = instruction.parameters.GetString("Value");
                if (string.IsNullOrWhiteSpace(valueStr))
                    valueStr = instruction.parameters.GetString("value");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) &&
                    !string.IsNullOrWhiteSpace(propertyName))
                {
                    // Parse property to check for component prefix (e.g., "Light.Color")
                    string componentName = null;
                    string actualPropertyName = propertyName;
                    
                    int dotIndex = propertyName.IndexOf('.');
                    if (dotIndex > 0 && dotIndex < propertyName.Length - 1)
                    {
                        componentName = propertyName.Substring(0, dotIndex);
                        actualPropertyName = propertyName.Substring(dotIndex + 1);
                    }

                    // Auto-create space if it doesn't exist
                    if (FindSpace(CurrentSpaceName) == null)
                    {
                        AddSpace(CurrentSpaceName);
                    }

                    // Auto-create object if it doesn't exist
                    var space = FindSpace(CurrentSpaceName);
                    var obj = FindObjectInSpace(space, objectName);
                    if (obj == null)
                    {
                        obj = AddObject(CurrentSpaceName, objectName, null);
                    }

                    // Parse value
                    JToken tokenValue = null;
                    if (!string.IsNullOrWhiteSpace(valueStr))
                    {
                        try {
                            tokenValue = JToken.Parse(valueStr);
                        } catch {
                            tokenValue = valueStr;
                        }
                    }

                    // If component is specified (via "Component.Property" format), set on component
                    if (!string.IsNullOrWhiteSpace(componentName))
                    {
                        var comp = obj.Components?.Find(c => 
                            string.Equals(c.Description, componentName, StringComparison.OrdinalIgnoreCase));
                        
                        if (comp != null)
                        {
                            AddOrSetPropertyValue(comp, actualPropertyName, "", tokenValue);
                        }
                        else
                        {
                            // Component not found - set on object's properties as fallback
                            obj.Properties ??= new List<ValuePair>();
                            var existingProp = obj.Properties.Find(p => 
                                string.Equals(p.name, propertyName, StringComparison.OrdinalIgnoreCase));
                            
                            if (existingProp != null)
                            {
                                existingProp.value = tokenValue;
                            }
                            else
                            {
                                obj.Properties.Add(new ValuePair(propertyName, tokenValue));
                            }
                        }
                    }
                    else
                    {
                        // No component specified - set on object's properties
                        obj.Properties ??= new List<ValuePair>();
                        var existingProp = obj.Properties.Find(p => 
                            string.Equals(p.name, actualPropertyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingProp != null)
                        {
                            existingProp.value = tokenValue;
                        }
                        else
                        {
                            obj.Properties.Add(new ValuePair(actualPropertyName, tokenValue));
                        }
                    }
                }
            }
            else if (action == T2G.Actions.delete_object)
            {
                string objectName = instruction.parameters.GetString("Name");
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        RemoveObject(CurrentSpaceName, objectName);
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.goto_space)
            {
                string spaceName = instruction.parameters.GetString("spaceName");
                if (!string.IsNullOrWhiteSpace(spaceName))
                {
                    CurrentSpaceName = spaceName;
                }
            }
            else if (action == T2G.Actions.rename_space)
            {
                string newSpaceName = instruction.parameters.GetString("spaceName");
                if (!string.IsNullOrWhiteSpace(newSpaceName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    var space = FindSpace(CurrentSpaceName);
                    if (space != null)
                    {
                        string oldName = space.Name;
                        space.Name = newSpaceName;
                        if (string.Equals(CurrentSpaceName, oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentSpaceName = newSpaceName;
                        }
                        Debug.Log($"[GameDescManager] Renamed space: {oldName} -> {newSpaceName}");
                    }
                }
            }
            else if (action == T2G.Actions.attach_to)
            {
                // Support multiple parameter naming conventions
                string childName = instruction.parameters.GetString("source");
                string parentName = instruction.parameters.GetString("target");
                string boneName = instruction.parameters.GetString("bone");

                if (!string.IsNullOrWhiteSpace(childName) && !string.IsNullOrWhiteSpace(parentName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        // Auto-create space if it doesn't exist
                        if (FindSpace(CurrentSpaceName) == null)
                        {
                            AddSpace(CurrentSpaceName);
                        }

                        var space = FindSpace(CurrentSpaceName);
                        if (space == null) return;

                        // Find or create child object
                        var child = FindObjectInSpace(space, childName);
                        if (child == null)
                        {
                            child = AddObject(CurrentSpaceName, childName, null);
                        }

                        // Find or create parent object
                        var parent = FindObjectInSpace(space, parentName);
                        if (parent == null)
                        {
                            parent = AddObject(CurrentSpaceName, parentName, null);
                        }

                        // Remove child from its current parent (if any)
                        if (child.Parent != null && child.Parent.Children != null)
                        {
                            child.Parent.Children.Remove(child);
                        }
                        else
                        {
                            // Child might be at root level (space.Objects), remove from there
                            space.Objects?.Remove(child);
                        }

                        // Add child to new parent's Children list
                        parent.Children ??= new List<Object>();
                        child.Parent = parent;
                        parent.Children.Add(child);

                        // If bone is specified, store it as a property on the child
                        if (!string.IsNullOrWhiteSpace(boneName))
                        {
                            child.Properties ??= new List<ValuePair>();
                            var boneProp = child.Properties.Find(p => string.Equals(p.name, "bone", StringComparison.OrdinalIgnoreCase));
                            if (boneProp != null)
                            {
                                boneProp.value = boneName;
                            }
                            else
                            {
                                child.Properties.Add(new ValuePair("bone", boneName));
                            }
                        }
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.remove_component)
            {
                string objectName = instruction.parameters.GetString("Name");
                string componentType = instruction.parameters.GetString("Type");
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try
                    {
                        RemoveComponent(CurrentSpaceName, objectName, componentType);
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.detach_from)
            {
                string objectName = instruction.parameters.GetString("Name");
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = instruction.parameters.GetString("childName");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        var space = FindSpace(CurrentSpaceName);
                        if (space == null) return;

                        var obj = FindObjectInSpace(space, objectName);
                        if (obj == null) return;

                        // If object has a parent, remove from parent's Children
                        if (obj.Parent != null)
                        {
                            obj.Parent.Children?.Remove(obj);
                        }
                        else
                        {
                            // Object is at root level, just remove from parent's check
                        }

                        // Set Parent to null and add to root (space.Objects)
                        obj.Parent = null;
                        space.Objects ??= new List<Object>();
                        if (!space.Objects.Contains(obj))
                        {
                            space.Objects.Add(obj);
                        }

                        // Remove bone property if exists
                        if (obj.Properties != null)
                        {
                            obj.Properties.RemoveAll(p => string.Equals(p.name, "bone", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch { }
                }
            }
        }

        public bool CreateFromGameDesc(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);

            if (!LoadGameDesc(filePath))
                return false;

            if (GameDescParser.ParseForInstructions(Snapshot, out var instructions))
            {
                return true;
            }

            return false;
        }

        public T2G.Instruction[] GetInstructionsFromGameDesc(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);

            if (!LoadGameDesc(filePath))
                return null;

            if (GameDescParser.ParseForInstructions(Snapshot, out var instructions))
            {
                return instructions;
            }

            return null;
        }

        // ============================================================
        // Domain: Snapshot lifecycle
        // ============================================================

        public GameDesc CreateGameDesc(string projectName, string title = null)
        {
            Snapshot = new GameDesc
            {
                ProjectName = projectName ?? "Untitled",
                Title = title ?? "Untitled",
                Spaces = new List<T2G.Assistant.Space>(),
                InstructionHistory = new List<InstructionRecord>()
            };

            return Snapshot;
        }

        public string SaveGameDesc(string fileName = null)
        {
            EnsureSnapshot();

            var wrapper = new GameDescFile
            {
                Context = Assistant.Instance.GameProject,
                GameDesc = Snapshot
            };

            string json = JsonConvert.SerializeObject(wrapper, _jsonSettings);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = MakeSafeFileName(Snapshot.Title);
            }
            
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            string fullPath = Path.Combine(_saveGameDescFolder, fileName);
            File.WriteAllText(fullPath, json);
            
            return fullPath;
        }

        public bool LoadGameDesc(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is null/empty.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc json file not found.", filePath);

            string json = File.ReadAllText(filePath);

            GameDescFile wrapper;
            try
            {
                wrapper = JsonConvert.DeserializeObject<GameDescFile>(json, _jsonSettings);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to deserialize GameDescFile: {e.Message}", e);
            }

            if (wrapper?.GameDesc == null)
                throw new InvalidOperationException("Invalid file: GameDesc missing.");

            Snapshot = wrapper.GameDesc;

            // Rebuild parent pointers for hierarchy correctness.
            RebuildParents(Snapshot);

            // Normalize lists/caches.
            Normalize(Snapshot);

            return true;
        }

        public List<string> ListSavedGameDescs()
        {
            Directory.CreateDirectory(_saveGameDescFolder);

            var files = new DirectoryInfo(_saveGameDescFolder)
                .GetFiles("*.json", SearchOption.TopDirectoryOnly);

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

            var result = new List<string>(files.Length);
            foreach (var f in files)
                result.Add(f.FullName);

            return result;
        }

        // ============================================================
        // Domain: Snapshot edits
        // ============================================================

        public Space AddSpace(string spaceName)
        {
            EnsureSnapshot();
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");

            Snapshot.Spaces ??= new List<Space>();

            var space = new Space
            {
                Name = spaceName,
                Components = new List<Component>(),
                Objects = new List<Object>()
            };

            Snapshot.Spaces.Add(space);
            return space;
        }

        public Object AddObject(string spaceName, string objectName, string desc = null, string parentName = null)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName)
                        ?? throw new InvalidOperationException($"Space '{spaceName}' not found.");

            var newObj = new Object
            {
                Name = objectName,
                Desc = desc ?? string.Empty,
                Components = new List<Component>(),
                Children = new List<Object>()
            };

            if (string.IsNullOrWhiteSpace(parentName))
            {
                space.Objects ??= new List<Object>();
                newObj.Parent = null;
                space.Objects.Add(newObj);
            }
            else
            {
                var parent = FindObjectInSpace(space, parentName)
                             ?? throw new InvalidOperationException($"Parent '{parentName}' not found in space '{spaceName}'.");

                parent.Children ??= new List<Object>();
                newObj.Parent = parent;
                parent.Children.Add(newObj);
            }

            return newObj;
        }

        public bool RemoveObject(string spaceName, string objectName)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName);
            if (space == null) return false;

            // Check if it's the space itself
            if (string.Equals(space.Name, objectName, StringComparison.OrdinalIgnoreCase))
                return false;

            // First search in space.Objects (top-level objects)
            if (space.Objects != null)
            {
                var target = space.Objects.Find(o => 
                    o != null && string.Equals(o.Name, objectName, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    space.Objects.Remove(target);
                    return true;
                }
            }

            // Then search recursively (children)
            var found = FindObjectInSpace(space, objectName);
            if (found == null) return false;

            var parent = found.Parent;
            if (parent?.Children == null) return false;

            return parent.Children.Remove(found);
        }

        public Component AddComponent(string spaceName, string objectName, string componentType, Instruction instruction)
        {
            EnsureSnapshot();

            var obj = RequireObject(spaceName, objectName);

            obj.Components ??= new List<Component>();

            var comp = new Component
            {
                Type = componentType,                   //file, component, asset
                Description = instruction.desc,
                BehaviorScript = string.Empty,
                Properties = new List<PropertyDesc>(),
                Assets = instruction.assets
            };

            if(string.Compare(comp.Type, "file", true) == 0 && File.Exists(comp.Description))
            {
                comp.BehaviorScript = File.ReadAllText(comp.Description);
            }
            else if (string.Compare(comp.Type, "script asset", true) == 0 && instruction.assets != null && instruction.assets.Count > 0)
            {
                comp.Assets = instruction.assets;
            }

            // Remove existing component with same type and assets before adding
            int existingIdx = obj.Components.FindIndex(c =>
                c != null &&
                string.Equals(c.Type, comp.Type, StringComparison.OrdinalIgnoreCase) &&
                ((c.Assets == null && comp.Assets == null) ||
                 (c.Assets != null && comp.Assets != null && c.Assets.SequenceEqual(comp.Assets))));
            if (existingIdx >= 0)
            {
                obj.Components.RemoveAt(existingIdx);
            }

            obj.Components.Add(comp);
            comp.RebuildPropertyMapIfExists();
            return comp;
        }

        public bool RemoveComponent(string spaceName, string objectName, string componentType)
        {
            EnsureSnapshot();

            var obj = RequireObject(spaceName, objectName);
            if (obj.Components == null) return false;

            int idx = obj.Components.FindIndex(c =>
                c != null && string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase));

            if (idx < 0) return false;

            obj.Components.RemoveAt(idx);
            return true;
        }

        public void SetBehaviorScript(string spaceName, string objectName, string componentType, string behaviorScript)
        {
            EnsureSnapshot();

            var comp = RequireComponent(spaceName, objectName, componentType);
            comp.BehaviorScript = behaviorScript ?? string.Empty;
        }

        public void AddOrSetPropertyValue(
            string spaceName,
            string objectName,
            string componentType,
            string propertyName,
            string propertyType,
            JToken value)
        {
            EnsureSnapshot();

            var comp = RequireComponent(spaceName, objectName, componentType);
            AddOrSetPropertyValue(comp, propertyName, propertyType, value);
        }

        public void AddOrSetPropertyValue(Component comp, string propertyName, string propertyType, JToken value)
        {
            comp.Properties ??= new List<PropertyDesc>();

            int idx = comp.Properties.FindIndex(p => p != null && string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            var prop = new PropertyDesc
            {
                Name = propertyName,
                Type = propertyType,
                Value = value
            };

            if (idx >= 0) 
                comp.Properties[idx] = prop;
            else 
                comp.Properties.Add(prop);

            comp.RebuildPropertyMapIfExists();
        }

        public void AddOrSetPropertyValue(
            string spaceName,
            string objectName,
            string componentType,
            string propertyName,
            string propertyType,
            object value)
        {
            AddOrSetPropertyValue(spaceName, objectName, componentType, propertyName, propertyType,
                value == null ? JValue.CreateNull() : JToken.FromObject(value));
        }

        public bool DeleteProperty(string spaceName, string objectName, string componentType, string propertyName)
        {
            EnsureSnapshot();

            var comp = RequireComponent(spaceName, objectName, componentType);
            if (comp.Properties == null) return false;

            int idx = comp.Properties.FindIndex(p =>
                p != null && string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (idx < 0) return false;

            comp.Properties.RemoveAt(idx);
            comp.RebuildPropertyMapIfExists();
            return true;
        }

        // ============================================================
        // Queries / Internal helpers
        // ============================================================

        private void EnsureSnapshot()
        {
            if (Snapshot == null)
                throw new InvalidOperationException("Snapshot is null. Call CreateNewSnapshot(...) or LoadSnapshot(...) first.");
        }

        private Space FindSpace(string spaceName)
        {
            if (Snapshot?.Spaces == null) return null;

            return Snapshot.Spaces.Find(s =>
                s != null && string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));
        }

        private Object RequireObject(string spaceName, string objectName)
        {
            var space = FindSpace(spaceName)
                        ?? throw new InvalidOperationException($"Space '{spaceName}' not found.");

            var obj = FindObjectInSpace(space, objectName)
                      ?? throw new InvalidOperationException($"Object '{objectName}' not found in space '{spaceName}'.");

            return obj;
        }

        private Component RequireComponent(string spaceName, string objectName, string componentType)
        {
            var obj = RequireObject(spaceName, objectName);

            if (obj.Components == null)
                throw new InvalidOperationException($"Object '{objectName}' has no components.");

            var comp = obj.Components.Find(c =>
                c != null && string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase));

            if (comp == null)
                throw new InvalidOperationException($"Component '{componentType}' not found on object '{objectName}'.");

            return comp;
        }

        private Object FindObjectInSpace(Space space, string objectName)
        {
            if (space == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            // First search top-level objects in space
            if (space.Objects != null)
            {
                foreach (var obj in space.Objects)
                {
                    if (obj != null && string.Equals(obj.Name, objectName, StringComparison.OrdinalIgnoreCase))
                        return obj;
                }
            }

            // Then search in children (recursive)
            var stack = new Stack<Object>();
            if (space.Objects != null)
            {
                foreach (var obj in space.Objects)
                {
                    if (obj?.Children != null)
                    {
                        for (int i = obj.Children.Count - 1; i >= 0; i--)
                            stack.Push(obj.Children[i]);
                    }
                }
            }

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null) continue;

                if (string.Equals(cur.Name, objectName, StringComparison.OrdinalIgnoreCase))
                    return cur;

                if (cur.Children != null)
                {
                    for (int i = cur.Children.Count - 1; i >= 0; i--)
                        stack.Push(cur.Children[i]);
                }
            }

            return null;
        }

        private static void Normalize(GameDesc gd)
        {
            gd.Spaces ??= new List<Space>();
            gd.InstructionHistory ??= new List<InstructionRecord>();

            foreach (var space in gd.Spaces)
            {
                if (space == null) continue;

                space.Objects ??= new List<Object>();
                space.Components ??= new List<Component>();

                foreach (var obj in space.Objects)
                {
                    NormalizeObjectRecursive(obj);
                }
            }
        }

        private static void NormalizeObjectRecursive(T2G.Assistant.Object obj)
        {
            if (obj == null) return;

            obj.Children ??= new List<Object>();
            obj.Components ??= new List<Component>();
            obj.Assets ??= new List<string>();

            foreach (var c in obj.Components)
            {
                if (c == null) continue;
                c.Properties ??= new List<PropertyDesc>();
                c.RebuildPropertyMapIfExists();
            }

            foreach (var child in obj.Children)
            {
                NormalizeObjectRecursive(child);
            }
        }

        private static void RebuildParents(GameDesc gd)
        {
            if (gd?.Spaces == null) return;

            foreach (var space in gd.Spaces)
            {
                if (space == null || space.Objects == null) continue;

                foreach (var obj in space.Objects)
                {
                    obj.Parent = null;
                    RebuildParentsRecursive(obj);
                }
            }
        }

        private static void RebuildParentsRecursive(Object parent)
        {
            if (parent?.Children == null) return;

            foreach (var child in parent.Children)
            {
                if (child == null) continue;

                child.Parent = parent;
                RebuildParentsRecursive(child);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "GameDesc";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        // ============================================================
        // File wrapper
        // ============================================================

        [Serializable]
        private class GameDescFile
        {
            public ProjectContext Context;
            public GameDesc GameDesc;
        }
    }

    /// <summary>
    /// Context metadata for a snapshot (separate from design graph).
    /// </summary>
    [Serializable]
    public class SnapshotContext
    {
        public string ProjectPath;
        public string LastFilePath;
        public DateTime CreatedUtc;
        public DateTime? LastSavedUtc;
    }

    /// <summary>
    /// Parses a GameDesc into flat instruction lists for batch execution.
    /// Generates create_object, set_property, add_component, and attach_to
    /// instructions that can recreate any space from its hierarchical data.
    /// </summary>
    public class GameDescParser
    {
        // ============================================================
        // Entry points
        // ============================================================

        /// <summary>
        /// Parse all spaces in a GameDesc into a flat instruction array.
        /// </summary>
        public static bool ParseForInstructions(GameDesc gameDesc, out Instruction[] instructions)
        {
            instructions = ParseForInstructions(gameDesc);
            return instructions != null;
        }

        /// <summary>
        /// Parse all spaces in a GameDesc into a flat instruction array.
        /// </summary>
        public static Instruction[] ParseForInstructions(GameDesc gameDesc)
        {
            if (gameDesc == null)
                return null;

            var result = new List<Instruction>();
            foreach (var space in gameDesc.Spaces)
            {
                if (space == null) continue;
                var spaceInstructions = ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        // ============================================================
        // Space → flat Instruction[]
        // ============================================================

        /// <summary>
        /// Parse a single space into a flat instruction list.
        /// Order: create_space, then each root object's subtree (DFS),
        /// then space-level components.
        /// </summary>
        public static Instruction[] ParseSpaceForInstructions(T2G.Assistant.Space space)
        {
            if (space == null) return null;

            var result = new List<Instruction>();

            // 1. create_space
            var spaceInstr = new Instruction
            {
                action = T2G.Actions.create_space,
                state = Instruction.eState.Resolved
            };
            spaceInstr.parameters = new List<ValuePair>
            {
                new ValuePair("SpaceName", space.Name)
            };
            result.Add(spaceInstr);

            // 2. Each root object's subtree
            foreach (var obj in space.Objects)
            {
                if (obj == null) continue;
                result.AddRange(ParseObjectForInstructions(obj));
            }

            // 3. Space-level components
            foreach (var comp in space.Components)
            {
                if (comp == null) continue;
                result.Add(ParseSpaceComponentForInstructions(comp, space.Name));
            }

            return result.ToArray();
        }

        // ============================================================
        // Object → flat Instruction[] (subtree)
        // ============================================================

        /// <summary>
        /// Parse an object and its entire subtree into flat instructions.
        /// Order: create_object, set_property for each obj property,
        /// add_component for each component, then recursively for children,
        /// then attach_to for each child.
        /// </summary>
        public static Instruction[] ParseObjectForInstructions(T2G.Assistant.Object obj)
        {
            if (obj == null) return null;

            var result = new List<Instruction>();

            // 1. create_object (no Parent param — hierarchy built via attach_to)
            var createInstr = new Instruction
            {
                action = T2G.Actions.create_object,
                state = Instruction.eState.Resolved,
                desc = obj.Desc ?? string.Empty,
            };
            createInstr.parameters = new List<ValuePair>
            {
                new ValuePair("Name", obj.Name)
            };
            // Assets
            if (obj.Assets != null && obj.Assets.Count > 0)
            {
                createInstr.assets = new List<string>(obj.Assets);
            }
            result.Add(createInstr);

            // 2. Object-level properties → set_property
            if (obj.Properties != null)
            {
                foreach (var prop in obj.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("objName", obj.Name),
                        new ValuePair("Property", prop.name),
                        new ValuePair("Value", prop.value)
                    };
                    result.Add(setProp);
                }
            }

            // 3. Object-level components
            if (obj.Components != null)
            {
                foreach (var comp in obj.Components)
                {
                    if (comp == null) continue;
                    result.Add(ParseComponentForInstructions(comp, obj.Name));
                }
            }

            // 4. Children (recursive, depth-first)
            for (int i = 0; i < (obj.Children?.Count ?? 0); i++)
            {
                var child = obj.Children[i];
                if (child == null) continue;

                // Child's own instructions (create_object, set_property, components, grandchildren)
                var childInstructions = ParseObjectForInstructions(child);
                if (childInstructions != null)
                    result.AddRange(childInstructions);

                // attach_to: link child to this parent
                var attachInstr = new Instruction
                {
                    action = T2G.Actions.attach_to,
                    state = Instruction.eState.Resolved
                };
                attachInstr.parameters = new List<ValuePair>
                {
                    new ValuePair("source", child.Name),
                    new ValuePair("target", obj.Name)
                };
                // Check for bone property on child
                if (child.Properties != null)
                {
                    var boneProp = child.Properties.Find(p =>
                        p != null && string.Equals(p.name, "bone", StringComparison.OrdinalIgnoreCase));
                    if (boneProp != null && boneProp.value != null)
                    {
                        attachInstr.parameters.Add(new ValuePair("bone", boneProp.value.ToString()));
                    }
                }
                result.Add(attachInstr);
            }

            return result.ToArray();
        }

        // ============================================================
        // Component → Instruction (add_component with nested set_property)
        // ============================================================

        /// <summary>
        /// Parse an object-level component into an add_component instruction
        /// with nested set_property sub-instructions for each property.
        /// </summary>
        public static Instruction ParseComponentForInstructions(T2G.Assistant.Component component, string objectName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = Instruction.eState.Resolved,
                assets = component.Assets
            };
            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", objectName),
                new ValuePair("type", component.Type ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            // Properties → nested set_property instructions
            var subInstructions = new List<Instruction>();
            if (component.Properties != null)
            {
                foreach (var prop in component.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.Name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("Name", objectName),
                        // Use "ComponentType.PropertyName" format — handles component-qualified properties
                        new ValuePair("Property", $"{component.Type}.{prop.Name}"),
                        new ValuePair("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            if (subInstructions.Count > 0)
                instr.instructions = subInstructions.ToArray();

            return instr;
        }

        // ============================================================
        // Space-level component → Instruction
        // ============================================================

        /// <summary>
        /// Parse a space-level component (targets the space itself).
        /// </summary>
        public static Instruction ParseSpaceComponentForInstructions(T2G.Assistant.Component component, string spaceName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = Instruction.eState.Resolved
            };
            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", spaceName),
                new ValuePair("type", component.Type ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            // Properties → nested set_property
            var subInstructions = new List<Instruction>();
            if (component.Properties != null)
            {
                foreach (var prop in component.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.Name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("Name", spaceName),
                        new ValuePair("Property", $"{component.Type}.{prop.Name}"),
                        new ValuePair("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            if (subInstructions.Count > 0)
                instr.instructions = subInstructions.ToArray();

            return instr;
        }
    }
}


