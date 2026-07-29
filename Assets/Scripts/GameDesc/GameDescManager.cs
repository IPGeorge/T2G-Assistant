using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using T2G;
using System.Linq;
using System.Text.RegularExpressions;

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

        public void RecordInstruction(T2G.Instruction instruction, T2G.Response response, string[] responseParams)
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
                UpdateFromInstruction(instruction, response, responseParams);
            }

            SaveGameDesc();
        }

        private void UpdateFromInstruction(T2G.Instruction instruction, T2G.Response response, string[] responseParams)
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
                Debug.Log($"[GameDescManager] create_object: objectName={objectName}, CurrentSpaceName={CurrentSpaceName}");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    if (FindSpace(CurrentSpaceName) == null)
                        AddSpace(CurrentSpaceName);

                    string remoteId = response?.ObjectId;
                    try
                    {
                        var obj = AddObject(CurrentSpaceName, objectName, instruction.desc);
                        if (!string.IsNullOrEmpty(remoteId))
                            obj.Id = remoteId;
                        Debug.Log($"[GameDescManager] Created object: {objectName}");

                        // Tags
                        string tagsStr = instruction.parameters.GetString("Tags");
                        if (!string.IsNullOrWhiteSpace(tagsStr))
                        {
                            foreach (var tag in tagsStr.Split(','))
                            {
                                var trimmed = tag.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmed) && !obj.Tags.Contains(trimmed))
                                    obj.Tags.Add(trimmed);
                            }
                        }

                        // Roles
                        string rolesStr = instruction.parameters.GetString("Roles");
                        if (!string.IsNullOrWhiteSpace(rolesStr))
                        {
                            foreach (var role in rolesStr.Split(','))
                            {
                                var trimmed = role.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmed) && !obj.Roles.Contains(trimmed))
                                    obj.Roles.Add(trimmed);
                            }
                        }

                        // Assets — instruction.assets is paired: [import0, load0, import1, load1, ...]
                        if (instruction.assets != null)
                        {
                            obj.Assets ??= new List<string>();
                            var space = FindSpace(CurrentSpaceName);
                            for (int i = 0; i < instruction.assets.Count; i += 2)
                            {
                                string importPath = instruction.assets[i];
                                string loadPath = i + 1 < instruction.assets.Count
                                    ? instruction.assets[i + 1] : importPath;
                                string key = AddAssetToSpace(importPath, loadPath, space);
                                if (key != null && !obj.Assets.Contains(key))
                                    obj.Assets.Add(key);
                            }
                        }

                        // Position from parameters
                        string positionStr = instruction.parameters.GetString("position");
                        if (!string.IsNullOrWhiteSpace(positionStr))
                        {
                            obj.Properties ??= new List<ValuePair>();
                            var posProp = obj.Properties.Find(p => string.Equals(p.name, "position", StringComparison.OrdinalIgnoreCase));
                            if (posProp != null) posProp.value = positionStr;
                            else obj.Properties.Add(new ValuePair("position", positionStr));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GameDescManager] Failed to create object: {ex.Message}");
                        var obj = AddObject(CurrentSpaceName, objectName, instruction.desc);
                        if (!string.IsNullOrEmpty(remoteId))
                            obj.Id = remoteId;
                    }
                }
                else
                {
                    Debug.LogWarning($"[GameDescManager] create_object skipped - objectName or CurrentSpaceName is empty");
                }
            }
            else if (action == T2G.Actions.add_script)
            {
                string objectName = instruction.parameters.GetString("objName");
                if (string.IsNullOrWhiteSpace(objectName))
                    objectName = instruction.parameters.GetString("ObjName");
                string componentType = instruction.parameters.GetString("type");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try { AddComponent(CurrentSpaceName, objectName, componentType, instruction); }
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
                    string componentName = null;
                    string actualPropertyName = propertyName;

                    int dotIndex = propertyName.IndexOf('.');
                    if (dotIndex > 0 && dotIndex < propertyName.Length - 1)
                    {
                        componentName = propertyName.Substring(0, dotIndex);
                        actualPropertyName = propertyName.Substring(dotIndex + 1);
                    }

                    if (FindSpace(CurrentSpaceName) == null)
                        AddSpace(CurrentSpaceName);

                    var space = FindSpace(CurrentSpaceName);
                    var obj = FindObjectInSpace(space, objectName);
                    if (obj == null)
                        obj = AddObject(CurrentSpaceName, objectName, null);

                    JToken tokenValue = null;
                    if (!string.IsNullOrWhiteSpace(valueStr))
                    {
                        try { tokenValue = JToken.Parse(valueStr); }
                        catch { tokenValue = valueStr; }
                    }

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
                            obj.Properties ??= new List<ValuePair>();
                            var existingProp = obj.Properties.Find(p =>
                                string.Equals(p.name, propertyName, StringComparison.OrdinalIgnoreCase));
                            if (existingProp != null) existingProp.value = tokenValue;
                            else obj.Properties.Add(new ValuePair(propertyName, tokenValue));
                        }
                    }
                    else
                    {
                        obj.Properties ??= new List<ValuePair>();
                        var existingProp = obj.Properties.Find(p =>
                            string.Equals(p.name, actualPropertyName, StringComparison.OrdinalIgnoreCase));
                        if (existingProp != null) existingProp.value = tokenValue;
                        else obj.Properties.Add(new ValuePair(actualPropertyName, tokenValue));
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
                        var space = FindSpace(CurrentSpaceName);
                        if (space == null) return;

                        space._nameToId.TryGetValue(objectName, out var targetId);

                        foreach (var obj in space.Objects.Values)
                        {
                            if (obj?.Relationships == null) continue;
                            if (targetId != null)
                                obj.Relationships.RemoveAll(r =>
                                    string.Equals(r.Target, targetId, StringComparison.OrdinalIgnoreCase));
                        }

                        RemoveObject(CurrentSpaceName, objectName);
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.goto_space)
            {
                string spaceName = instruction.parameters.GetString("spaceName");
                if (!string.IsNullOrWhiteSpace(spaceName))
                    CurrentSpaceName = spaceName;
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
                            CurrentSpaceName = newSpaceName;
                        Debug.Log($"[GameDescManager] Renamed space: {oldName} -> {newSpaceName}");
                    }
                }
            }
            else if (action == T2G.Actions.attach_to)
            {
                string childName = instruction.parameters.GetString("source");
                string parentName = instruction.parameters.GetString("target");
                string socketName = instruction.parameters.GetString("socket");

                if (!string.IsNullOrWhiteSpace(childName) && !string.IsNullOrWhiteSpace(parentName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        if (FindSpace(CurrentSpaceName) == null)
                            AddSpace(CurrentSpaceName);

                        var space = FindSpace(CurrentSpaceName);
                        if (space == null) return;

                        var child = FindObjectInSpace(space, childName);
                        if (child == null)
                            child = AddObject(CurrentSpaceName, childName, null);

                        var parent = FindObjectInSpace(space, parentName);
                        if (parent == null)
                            parent = AddObject(CurrentSpaceName, parentName, null);

                        // Remove any existing containment/attachment relationship from this child
                        child.Relationships ??= new List<Relationship>();
                        child.Relationships.RemoveAll(r =>
                            string.Equals(r.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(r.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase));

                        // Add new relationship with parent's GUID
                        child.Relationships.Add(new Relationship
                        {
                            Type = GameDescRelationTypes.AttachedTo,
                            Target = parent.Id,
                            Slot = socketName ?? string.Empty
                        });

                        if (responseParams != null)
                        {
                            foreach (var responseParam in responseParams)
                            {
                                string[] valuePaire = responseParam.Split(new char[] { '=' }, 2);
                                if (valuePaire.Length == 2)
                                    AddOrSetProperty(child, valuePaire[0], valuePaire[1]);
                            }

                            child.Properties?.RemoveAll(p => string.Equals(p.name, "position", StringComparison.OrdinalIgnoreCase)
                                                          || string.Equals(p.name, "rotation", StringComparison.OrdinalIgnoreCase)
                                                          || string.Equals(p.name, "scale", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.set_relationship)
            {
                string sourceName = instruction.parameters.GetString("source");
                string relType = instruction.parameters.GetString("type");
                string targetName = instruction.parameters.GetString("target");
                string slotName = instruction.parameters.GetString("slot");

                if (!string.IsNullOrWhiteSpace(sourceName) && !string.IsNullOrWhiteSpace(relType) &&
                    !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        if (FindSpace(CurrentSpaceName) == null)
                            AddSpace(CurrentSpaceName);

                        var space = FindSpace(CurrentSpaceName);
                        if (space == null) return;

                        var source = FindObjectInSpace(space, sourceName);
                        if (source == null)
                            source = AddObject(CurrentSpaceName, sourceName, null);

                        source.Relationships ??= new List<Relationship>();

                        // Resolve target name to GUID
                        string targetId = string.Empty;
                        if (!string.IsNullOrWhiteSpace(targetName))
                        {
                            if (space._nameToId.TryGetValue(targetName, out var tid))
                                targetId = tid;
                            else
                            {
                                var targetObj = AddObject(CurrentSpaceName, targetName, null);
                                targetId = targetObj.Id;
                            }
                        }

                        // Remove existing relationship of the same type targeting the same object
                        source.Relationships.RemoveAll(r =>
                            string.Equals(r.Type, relType, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(r.Target, targetId, StringComparison.OrdinalIgnoreCase));

                        source.Relationships.Add(new Relationship
                        {
                            Type = relType,
                            Target = targetId,
                            Slot = slotName ?? string.Empty
                        });
                    }
                    catch { }
                }
            }
            else if (action == T2G.Actions.remove_script)
            {
                string objectName = instruction.parameters.GetString("Name");
                string componentType = instruction.parameters.GetString("Type");
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) && !string.IsNullOrWhiteSpace(componentType))
                {
                    try { RemoveComponent(CurrentSpaceName, objectName, componentType); }
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

                        // Remove containment/attachment relationships
                        obj.Relationships ??= new List<Relationship>();
                        obj.Relationships.RemoveAll(r =>
                            string.Equals(r.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(r.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase));

                        if (responseParams != null)
                        {
                            foreach (var responseParam in responseParams)
                            {
                                string[] valuePaire = responseParam.Split(new char[] { '=' }, 2);
                                if (valuePaire.Length == 2)
                                    AddOrSetProperty(obj, valuePaire[0], valuePaire[1]);
                            }

                            obj.Properties?.RemoveAll(p => string.Equals(p.name, "localPosition", StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(p.name, "localRotation", StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(p.name, "localScale", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch { }
                }
            }
        }

        private static void AddOrSetProperty(T2G.Assistant.Object obj, string name, JToken value)
        {
            if (obj == null) return;
            obj.Properties ??= new List<ValuePair>();
            var existing = obj.Properties.Find(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                existing.value = value;
            else
                obj.Properties.Add(new ValuePair(name, value));
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
                SchemaVersion = 1,
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

            // Try new flat format first (SchemaVersion >= 1)
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

            if (wrapper.SchemaVersion == null || wrapper.SchemaVersion < 1)
            {
                // Legacy hierarchical format — migrate
                var legacyWrapper = JsonConvert.DeserializeObject<LegacyGameDescFile>(json, _jsonSettings);
                if (legacyWrapper?.GameDesc == null)
                    throw new InvalidOperationException("Invalid file: cannot migrate legacy GameDesc.");

                Snapshot = MigrateFromLegacy(legacyWrapper.GameDesc);
                Debug.Log("[GameDescManager] Migrated legacy hierarchical GameDesc to flat format.");

                // Re-save in new format immediately
                SaveGameDesc(Path.GetFileName(filePath));
            }
            else
            {
                Snapshot = wrapper.GameDesc;
            }

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

        // ============================================================
        // Export / Import (human-oriented format)
        // ============================================================

        /// <summary>
        /// Exports the current Snapshot as a hierarchical HumanGameDesc.
        /// </summary>
        public HumanGameDesc ExportToHumanGameDesc()
        {
            EnsureSnapshot();
            return GameDescConverter.ToHuman(Snapshot);
        }

        /// <summary>
        /// Saves the current Snapshot as a human-oriented JSON file.
        /// </summary>
        public string SaveHumanGameDesc(string filePath)
        {
            EnsureSnapshot();
            var human = ExportToHumanGameDesc();
            string json = JsonConvert.SerializeObject(human, _jsonSettings);
            File.WriteAllText(filePath, json);
            return filePath;
        }

        /// <summary>
        /// Loads a human-oriented (hierarchical) GameDesc JSON file
        /// and converts it to the internal flat model.
        /// </summary>
        public void ImportFromHumanGameDesc(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string json = File.ReadAllText(filePath);
            var human = JsonConvert.DeserializeObject<HumanGameDesc>(json, _jsonSettings);
            if (human == null)
                throw new InvalidOperationException("Failed to deserialize HumanGameDesc.");

            Snapshot = GameDescConverter.FromHuman(human);
            Normalize(Snapshot);
        }

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
                Objects = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase)
            };

            Snapshot.Spaces.Add(space);
            return space;
        }

        public Object AddObject(string spaceName, string objectName, string desc = null)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName)
                        ?? throw new InvalidOperationException($"Space '{spaceName}' not found.");

            var newObj = new Object
            {
                Id = Guid.NewGuid().ToString(),
                Name = objectName,
                Desc = desc ?? string.Empty,
                Components = new List<Component>()
            };

            space.Objects[newObj.Id] = newObj;
            space._nameToId[objectName] = newObj.Id;

            return newObj;
        }

        public bool RemoveObject(string spaceName, string objectName)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName);
            if (space == null) return false;

            if (space._nameToId.TryGetValue(objectName, out var id))
            {
                space.Objects.Remove(id);
                space._nameToId.Remove(objectName);
                return true;
            }
            return false;
        }

        public Component AddComponent(string spaceName, string objectName, string componentType, Instruction instruction)
        {
            EnsureSnapshot();

            var obj = RequireObject(spaceName, objectName);

            obj.Components ??= new List<Component>();

            var comp = new Component
            {
                Type = componentType,
                SourceType = componentType,
                Description = instruction.desc,
                Properties = new List<PropertyDesc>(),
                Assets = instruction.assets
            };

            if(string.Compare(componentType, "file", true) == 0)
            {
                string className = null;

                if (File.Exists(comp.Description))
                {
                    string scriptContent = File.ReadAllText(comp.Description);
                    className = ExtractScriptClassName(scriptContent);
                }

                className ??= Path.GetFileNameWithoutExtension(comp.Description);

                if (!string.IsNullOrWhiteSpace(className))
                {
                    comp.Type = className;

                    string importPath = comp.Description;
                    var space = FindSpace(spaceName);
                    if (space != null)
                    {
                        space.Assets ??= new Dictionary<string, AssetInfo>();
                        if (!space.Assets.ContainsKey(importPath))
                        {
                            space.Assets[importPath] = new AssetInfo
                            {
                                ImportPath = importPath,
                                LoadPath = className,
                                Type = "Script"
                            };
                        }
                    }
                }
            }
            else if (string.Compare(componentType, "asset", true) == 0 && instruction.assets != null && instruction.assets.Count > 0)
            {
                comp.Assets = instruction.assets;

                var space = FindSpace(spaceName);
                if (space != null)
                {
                    space.Assets ??= new Dictionary<string, AssetInfo>();
                    for (int i = 0; i < instruction.assets.Count; i++)
                    {
                        string assetPath = instruction.assets[i];
                        if (string.IsNullOrWhiteSpace(assetPath)) continue;

                        string className = null;
                        string fullPath = Path.Combine(Application.dataPath, assetPath);
                        if (File.Exists(fullPath))
                        {
                            string content = File.ReadAllText(fullPath);
                            className = ExtractScriptClassName(content);
                        }
                        className ??= Path.GetFileNameWithoutExtension(assetPath);
                        if (string.IsNullOrWhiteSpace(className)) continue;

                        if (i == 0)
                            comp.Type = className;

                        if (!space.Assets.ContainsKey(assetPath))
                        {
                            space.Assets[assetPath] = new AssetInfo
                            {
                                ImportPath = assetPath,
                                LoadPath = className,
                                Type = "Script"
                            };
                        }
                    }
                }
            }
            else
            {
                comp.Type = instruction.desc ?? componentType;
            }

            // Prevent duplicate component types
            bool duplicate = obj.Components.Any(c =>
                c != null &&
                string.Equals(c.Type, comp.Type, StringComparison.OrdinalIgnoreCase));
            if (duplicate) return comp;

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

            if (space._nameToId.TryGetValue(objectName, out var id) &&
                space.Objects.TryGetValue(id, out var obj))
                return obj;

            return null;
        }

        private static void Normalize(GameDesc gd)
        {
            gd.Spaces ??= new List<Space>();
            gd.InstructionHistory ??= new List<InstructionRecord>();

            foreach (var space in gd.Spaces)
            {
                if (space == null) continue;

                space.Objects ??= new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
                space.Components ??= new List<Component>();
                space.Assets ??= new Dictionary<string, AssetInfo>();
                space.RebuildNameIndex();

                foreach (var obj in space.Objects.Values)
                {
                    if (obj == null) continue;

                    obj.Tags ??= new List<string>();
                    obj.Roles ??= new List<string>();
                    obj.Relationships ??= new List<Relationship>();
                    obj.Components ??= new List<Component>();
                    obj.Assets ??= new List<string>();

                    // Migrate any legacy comma-delimited asset strings ("key,LoadPath")
                    // into Space.Assets, so obj.Assets stores only the key.
                    for (int i = 0; i < obj.Assets.Count; i++)
                    {
                        string assetStr = obj.Assets[i];
                        // If it contains a comma it's still in legacy format, migrate it.
                        // We also migrate if the key doesn't exist in space.Assets yet.
                        if (assetStr.IndexOf(',') >= 0 || !space.Assets.ContainsKey(assetStr))
                        {
                            string key = AddAssetToSpace(assetStr, space);
                            if (key != null)
                                obj.Assets[i] = key;
                            else
                                obj.Assets.RemoveAt(i--);
                        }
                    }

                    foreach (var c in obj.Components)
                    {
                        if (c == null) continue;
                        c.Properties ??= new List<PropertyDesc>();
                        c.RebuildPropertyMapIfExists();
                    }
                }
            }
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "GameDesc";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        /// <summary>
        /// Extract the MonoBehaviour class name from a script's source text.
        /// </summary>
        private static string ExtractScriptClassName(string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent)) return null;
            var match = Regex.Match(scriptContent, @"class\s+(\w+)\s*:\s*MonoBehaviour");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Infer asset type from its file extension (returned without the leading dot).
        /// </summary>
        private static string InferAssetType(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) return "";
            return ext.TrimStart('.');
        }

        /// <summary>
        /// Add an asset entry to Space.Assets or return existing key.
        /// </summary>
        private static string AddAssetToSpace(string importPath, string loadPath, Space space)
        {
            if (string.IsNullOrWhiteSpace(importPath)) return null;

            string key = importPath.Trim();
            string lp = !string.IsNullOrWhiteSpace(loadPath) ? loadPath.Trim() : key;

            if (!space.Assets.ContainsKey(key))
            {
                space.Assets[key] = new AssetInfo
                {
                    ImportPath = key,
                    LoadPath = lp,
                    Type = InferAssetType(key)
                };
            }

            return key;
        }

        /// <summary>
        /// Parse a single asset string from 'Object.Assets' (legacy format "import,load") into
        /// separate import/load paths. If no comma, the entire string is used as both.
        /// </summary>
        private static string AddAssetToSpace(string assetStr, Space space)
        {
            if (string.IsNullOrWhiteSpace(assetStr)) return null;
            var parts = assetStr.Split(new[] { ',' }, 2);
            return AddAssetToSpace(parts[0].Trim(),
                parts.Length > 1 ? parts[1].Trim() : parts[0].Trim(),
                space);
        }

        // ============================================================
        // File wrapper
        // ============================================================

        [Serializable]
        private class GameDescFile
        {
            public int? SchemaVersion;
            public ProjectContext Context;
            public GameDesc GameDesc;
        }

        // ============================================================
        // Legacy migration DTOs (old hierarchical format, schema v0)
        // ============================================================

        [Serializable]
        private class LegacyGameDescFile
        {
            public ProjectContext Context;
            public LegacyGameDesc GameDesc;
        }

        [Serializable]
        private class LegacyGameDesc
        {
            public string ProjectName;
            public string Title;
            public List<LegacySpace> Spaces;
            public List<InstructionRecord> InstructionHistory;
        }

        [Serializable]
        private class LegacySpace
        {
            public string Name;
            public List<Component> Components;
            public List<LegacyObject> Objects;
        }

        [Serializable]
        private class LegacyObject
        {
            public string Name;
            public string Desc;
            public List<ValuePair> Properties;
            public List<string> Assets;
            public List<Component> Components;
            public List<LegacyObject> Children;
            public string Socket;
        }

        private static GameDesc MigrateFromLegacy(LegacyGameDesc legacy)
        {
            var flat = new GameDesc
            {
                ProjectName = legacy.ProjectName,
                Title = legacy.Title,
                InstructionHistory = legacy.InstructionHistory ?? new List<InstructionRecord>(),
                Spaces = new List<Space>()
            };

            if (legacy.Spaces == null) return flat;

            foreach (var legacySpace in legacy.Spaces)
            {
                if (legacySpace == null) continue;

                var space = new Space
                {
                    Name = legacySpace.Name,
                    Components = legacySpace.Components ?? new List<Component>(),
                    Objects = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase)
                };

                FlattenLegacyObjects(legacySpace.Objects, null, space);

                flat.Spaces.Add(space);
            }

            return flat;
        }

        private static void FlattenLegacyObjects(List<LegacyObject> legacyObjects, string parentName, Space space)
        {
            if (legacyObjects == null) return;

            foreach (var legacyObj in legacyObjects)
            {
                if (legacyObj == null) continue;

                var objId = Guid.NewGuid().ToString();
                var legacyAssets = legacyObj.Assets ?? new List<string>();
                var migratedKeys = new List<string>(legacyAssets.Count);
                foreach (var a in legacyAssets)
                {
                    string key = AddAssetToSpace(a, space);
                    if (key != null) migratedKeys.Add(key);
                }

                var obj = new Object
                {
                    Id = objId,
                    Name = legacyObj.Name,
                    Desc = legacyObj.Desc ?? string.Empty,
                    Tags = new List<string>(),
                    Roles = new List<string>(),
                    Relationships = new List<Relationship>(),
                    Properties = legacyObj.Properties ?? new List<ValuePair>(),
                    Assets = migratedKeys,
                    Components = legacyObj.Components ?? new List<Component>()
                };

                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    string parentId = space._nameToId.TryGetValue(parentName, out var pid)
                        ? pid : parentName;
                    obj.Relationships.Add(new Relationship
                    {
                        Type = GameDescRelationTypes.Contains,
                        Target = parentId,
                        Slot = legacyObj.Socket ?? string.Empty
                    });
                }

                space.Objects[objId] = obj;
                space._nameToId[obj.Name] = objId;

                if (legacyObj.Children != null && legacyObj.Children.Count > 0)
                {
                    FlattenLegacyObjects(legacyObj.Children, legacyObj.Name, space);
                }
            }
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
}


