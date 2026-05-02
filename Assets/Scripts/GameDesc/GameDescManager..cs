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
                Debug.Log($"[GameDescManager] create_object: objectName={objectName}, desc={instruction?.desc}, CurrentSpaceName={CurrentSpaceName}");
                
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
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GameDescManager] Failed to create object: {ex.Message}");
                        AddObject(CurrentSpaceName, objectName, instruction.desc, null);
                    }

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
                }
                else
                {
                    Debug.LogWarning($"[GameDescManager] create_object skipped - objectName or CurrentSpaceName is empty");
                }
            }
            else if (action == T2G.Actions.add_component)
            {
                string objectName = instruction.parameters.GetString("Name");
                string componentType = instruction.parameters.GetString("Type");
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try
                    {
                        AddComponent(CurrentSpaceName, objectName, componentType);
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
