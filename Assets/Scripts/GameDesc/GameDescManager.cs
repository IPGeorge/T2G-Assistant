using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using T2G;

namespace T2G.Assistant
{
    /// <summary>
    /// Manages GameDesc design snapshots: create, save, load, list, and edit.
    /// Snapshot is the domain model (design-state), not runtime state.
    /// </summary>
    public sealed class GameDescManager
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
                                gameObj.Properties.Add(new ValuePair("position", positionStr));
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
                string objectName = instruction.parameters.GetString("Name");
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = instruction.parameters.GetString("objName");
                
                string componentType = instruction.parameters.GetString("Type");
                if (string.IsNullOrWhiteSpace(componentType))
                    componentType = instruction.parameters.GetString("component");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try
                    {
                        AddComponent(CurrentSpaceName, objectName, componentType);
                        
                        // Populate BehaviorScript from instruction.assets (resolved asset path)
                        var space = FindSpace(CurrentSpaceName);
                        var obj = FindObjectInSpace(space, objectName);
                        if (obj != null && obj.Components != null && obj.Components.Count > 0)
                        {
                            var comp = obj.Components[obj.Components.Count - 1];
                            if (instruction.assets != null && instruction.assets.Count > 0)
                            {
                                comp.BehaviorScript = instruction.assets[0];
                            }
                        }
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.set_property)
            {
                // Support both parameter naming conventions: "Name"/"Property"/"Value" and "objName"/"property"/"value"
                string objectName = instruction.parameters.GetString("Name");
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = instruction.parameters.GetString("objName");
                
                string propertyName = instruction.parameters.GetString("Property");
                if (string.IsNullOrWhiteSpace(propertyName))
                    propertyName = instruction.parameters.GetString("property");
                
                string valueStr = instruction.parameters.GetString("Value");
                if (string.IsNullOrWhiteSpace(valueStr))
                    valueStr = instruction.parameters.GetString("value");

                // Debug removed for cleaner output

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) &&
                    !string.IsNullOrWhiteSpace(propertyName))
                {
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

                    // Add or update property on the object
                    obj.Properties ??= new List<ValuePair>();
                    var existingProp = obj.Properties.Find(p => string.Equals(p.name, propertyName, StringComparison.OrdinalIgnoreCase));
                    
                    JToken tokenValue = null;
                    if (!string.IsNullOrWhiteSpace(valueStr))
                    {
                        try {
                            tokenValue = JToken.Parse(valueStr);
                        } catch {
                            tokenValue = valueStr;
                        }
                    }
                    
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
            else if (action == T2G.Actions.detach)
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

        public Component AddComponent(string spaceName, string objectName, string componentType)
        {
            EnsureSnapshot();

            var obj = RequireObject(spaceName, objectName);

            obj.Components ??= new List<Component>();

            var comp = new Component
            {
                Type = componentType,
                Properties = new List<PropertyDesc>(),
                BehaviorScript = string.Empty
            };

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
            comp.Properties ??= new List<PropertyDesc>();

            int idx = comp.Properties.FindIndex(p =>
                p != null && string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            var prop = new PropertyDesc
            {
                Name = propertyName,
                Type = propertyType,
                Value = value
            };

            if (idx >= 0) comp.Properties[idx] = prop;
            else comp.Properties.Add(prop);

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
    /// Parses a GameDesc for instructions
    /// </summary>
    public class GameDescParser
    {
        public static bool ParseForInstructions(GameDesc gameDesc, out Instruction[] instructions)
        {
            if(gameDesc == null)
            {
                instructions = null;
                return false;
            }
            
            List<Instruction> instructionList = new List<Instruction>();

            foreach(var space in gameDesc.Spaces)
            {
                var spaceInstruction = ParseSpaceForInstruction(space);
                if (spaceInstruction != null)
                {
                    instructionList.Add(spaceInstruction);
                }
            }
            instructions = instructionList.ToArray();

            return true;

        }


        private static Instruction ParseSpaceForInstruction(T2G.Assistant.Space space)
        {
            if (space == null)
            {
                return null;
            }

            Instruction instruction = new Instruction();
            instruction.action = T2G.Actions.create_space;
            instruction.parameters.Add(new ValuePair("Name", space.Name));

            List<Instruction> objInstructions = new List<Instruction>();
            foreach(T2G.Assistant.Object obj in space.Objects)
            {
                var objInstruction = ParseObjectForInstruction(obj);
                if(objInstruction != null)
                {
                    objInstructions.Add(objInstruction);
                }
            }
            instruction.instructions = objInstructions.ToArray();
            return instruction;
        }

        // -------------------------
        //Recursively parse objects. Has overflow risk.  
        // -------------------------
        private static Instruction ParseObjectForInstruction(T2G.Assistant.Object gameDescObject)
        {
            if(gameDescObject == null)
            {
                return null;
            }

            Instruction instruction = new Instruction();
            instruction.action = T2G.Actions.create_object;
            instruction.parameters.Add(new ValuePair("Name", gameDescObject.Name));
            instruction.desc = gameDescObject.Desc;

            if(string.IsNullOrEmpty(gameDescObject.Parent.Name))
            {
                instruction.parameters.Add(new ValuePair("Parent", gameDescObject.Parent.Name));
            }

            List<Instruction> instructionList = new List<Instruction>();


            foreach(var component in gameDescObject.Components)
            {
                if(component == null)
                {
                    continue;
                }
                var componentInstruction = ParseComponentForInstruction(component);
                if(componentInstruction != null)
                {
                    instructionList.Add(componentInstruction);
                }
            }

            foreach (var child in gameDescObject.Children)
            {
                if(child == null)
                {
                    continue;
                }
                var childInstruction = ParseObjectForInstruction(child);
                if (childInstruction != null)
                {
                    instructionList.Add(childInstruction);
                }
            }

            instruction.instructions = instructionList.ToArray();

            return instruction;
        }

        private static Instruction ParseComponentForInstruction(T2G.Assistant.Component component)
        {
            if(component == null)
            {
                return null;
            }
            Instruction instruction = new Instruction();
            instruction.action = T2G.Actions.add_component;

            //TODO
            //component.Type;
            //component.Properties
            //component.BehaviorScript
            //component.Assets

            
            return instruction;
        }
    }
}


