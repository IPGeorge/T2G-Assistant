using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        public string CurrentSpaceName { get; private set; }
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
                if (Snapshot.Spaces != null && Snapshot.Spaces.Count > 0)
                {
                    CurrentSpaceName = Snapshot.Spaces[0].Name;
                }
            }
            else
            {
                CreateGameDesc(projectName, title);
                SaveGameDesc(projectName);
            }
        }

        public void SetCurrentSpace(string spaceName)
        {
            CurrentSpaceName = spaceName;
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
            if (instruction == null || string.IsNullOrWhiteSpace(instruction.action))
                return;

            string action = instruction.action;

            if (action == T2G.Actions.create_space)
            {
                string spaceName = instruction.parameters.GetString("spaceName");
                if (!string.IsNullOrWhiteSpace(spaceName))
                {
                    AddSpace(spaceName, instruction.desc);
                    CurrentSpaceName = spaceName;
                }
            }
            else if (action == T2G.Actions.create_object)
            {
                string objectName = instruction.parameters.GetString("Name");
                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName))
                {
                    try
                    {
                        string parentName = instruction.parameters.GetString("Parent");
                        AddObject(CurrentSpaceName, objectName, instruction.desc, parentName);
                    }
                    catch
                    {
                        AddObject(CurrentSpaceName, objectName, instruction.desc, null);
                    }
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
                string objectName = instruction.parameters.GetString("Name");
                string componentType = instruction.parameters.GetString("Component");
                string propertyName = instruction.parameters.GetString("Property");
                string propertyType = instruction.parameters.GetString("Type");
                JToken value = instruction.parameters.GetValue("Value");

                if (!string.IsNullOrWhiteSpace(objectName) && !string.IsNullOrWhiteSpace(CurrentSpaceName) &&
                    !string.IsNullOrWhiteSpace(componentType) && !string.IsNullOrWhiteSpace(propertyName))
                {
                    try
                    {
                        AddOrSetPropertyValue(CurrentSpaceName, objectName, componentType, propertyName, propertyType, value);
                    }
                    catch { }
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
                Spaces = new List<T2G.Assistant.Object>(),
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

        public Object AddSpace(string spaceName, string desc = null)
        {
            EnsureSnapshot();
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");

            Snapshot.Spaces ??= new List<Object>();

            var space = new Object
            {
                Name = spaceName,
                Desc = desc ?? "Space",
                Components = new List<Component>(),
                Parent = null,
                Children = new List<Object>()
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
                space.Children ??= new List<Object>();
                newObj.Parent = space;
                space.Children.Add(newObj);
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

            if (string.Equals(space.Name, objectName, StringComparison.OrdinalIgnoreCase))
                return false;

            var target = FindObjectInSpace(space, objectName);
            if (target == null) return false;

            var parent = target.Parent;
            if (parent?.Children == null) return false;

            return parent.Children.Remove(target);
        }

        public Component AddComponent(string spaceName, string objectName, string componentType)
        {
            EnsureSnapshot();

            var obj = RequireObject(spaceName, objectName);

            obj.Components ??= new List<Component>();

            var comp = new Component
            {
                Type = componentType,
                Assets = new List<string>(),
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

        private Object FindSpace(string spaceName)
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

        private static Object FindObjectInSpace(Object spaceRoot, string objectName)
        {
            if (spaceRoot == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            var stack = new Stack<Object>();
            stack.Push(spaceRoot);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null) continue;

                if (string.Equals(cur.Name, objectName, StringComparison.OrdinalIgnoreCase))
                    return cur;

                if (cur.Children == null) continue;
                for (int i = cur.Children.Count - 1; i >= 0; i--)
                    stack.Push(cur.Children[i]);
            }

            return null;
        }

        private static void Normalize(GameDesc gd)
        {
            gd.Spaces ??= new List<Object>();
            gd.InstructionHistory ??= new List<InstructionRecord>();

            foreach (var space in gd.Spaces)
            {
                if (space == null) continue;

                space.Children ??= new List<Object>();
                space.Components ??= new List<Component>();

                NormalizeObjectRecursive(space);
            }
        }

        private static void NormalizeObjectRecursive(Object obj)
        {
            if (obj == null) return;

            obj.Children ??= new List<Object>();
            obj.Components ??= new List<Component>();

            foreach (var c in obj.Components)
            {
                if (c == null) continue;
                c.Assets ??= new List<string>();
                c.Properties ??= new List<PropertyDesc>();
                c.RebuildPropertyMapIfExists();
            }

            foreach (var child in obj.Children)
                NormalizeObjectRecursive(child);
        }

        private static void RebuildParents(GameDesc gd)
        {
            if (gd?.Spaces == null) return;

            foreach (var space in gd.Spaces)
            {
                if (space == null) continue;

                space.Parent = null;
                RebuildParentsRecursive(space);
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
                var spaceInstruction = ParseObjectForInstruction(space);
                if (spaceInstruction != null)
                {
                    instructionList.Add(spaceInstruction);
                }
            }
            instructions = instructionList.ToArray();

            return true;

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
