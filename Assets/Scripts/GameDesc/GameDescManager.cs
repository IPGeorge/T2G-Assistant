using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace T2G.Assistant
{
    /// <summary>
    /// Manages GameDesc snapshots (design-state), saving/loading to disk,
    /// and provides common update operations.
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
            _saveFolder = Path.Combine(Application.persistentDataPath, "GameDescs");
            Directory.CreateDirectory(_saveFolder);

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,

                // Avoid circular reference issues by NOT persisting Parent pointers.
                // We rebuild Parent after load.
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

                // Keep robust reading if schema evolves.
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        // -------------------------
        // Public State
        // -------------------------
        /// <summary>
        /// Path to the Unity project associated with the current GameDesc snapshot.
        /// Example: C:\UnityGames\Shooter
        /// </summary>
        public string ProjectPath { get; private set; }

        /// <summary>
        /// Current loaded/active GameDesc snapshot.
        /// </summary>
        public GameDesc Current { get; private set; }

        // -------------------------
        // Internal
        // -------------------------
        private readonly string _saveFolder;
        private readonly JsonSerializerSettings _jsonSettings;

        // ============================================================
        // Creation
        // ============================================================

        public GameDesc CreateNew(string title, string projectPath)
        {
            ProjectPath = projectPath ?? string.Empty;

            Current = new GameDesc
            {
                Title = title ?? "Untitled",
                Spaces = new List<Object>()
            };

            return Current;
        }

        // ============================================================
        // Save / Load
        // ============================================================

        /// <summary>
        /// Saves Current to persistent data path. Returns the full file path.
        /// If fileName is null, a timestamped name is generated.
        /// </summary>
        public string SaveToDisk(string fileName = null)
        {
            EnsureCurrent();

            // Wrap both ProjectPath and GameDesc in one file (so it travels together).
            var wrapper = new GameDescFile
            {
                ProjectPath = ProjectPath,
                GameDesc = Current
            };

            // IMPORTANT: Parent pointers are not persisted (ReferenceLoopHandling.Ignore).
            // We'll rebuild Parent from Children upon load.

            string json = JsonConvert.SerializeObject(wrapper, _jsonSettings);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                string safeTitle = MakeSafeFileName(Current.Title);
                fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }
            else if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            string fullPath = Path.Combine(_saveFolder, fileName);
            File.WriteAllText(fullPath, json);

            return fullPath;
        }

        /// <summary>
        /// Loads a GameDesc snapshot from a saved json file.
        /// Updates Current and ProjectPath. Returns Current.
        /// </summary>
        public GameDesc LoadFromDisk(string filePath)
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

            ProjectPath = wrapper.ProjectPath ?? string.Empty;
            Current = wrapper.GameDesc;

            // Rebuild parent pointers for hierarchy correctness.
            RebuildParents(Current);

            // Ensure child lists exist and component property maps are usable.
            Normalize(Current);

            return Current;
        }

        /// <summary>
        /// Returns a list of saved GameDesc json files (full paths), newest first.
        /// </summary>
        public List<string> ListSavedFiles()
        {
            Directory.CreateDirectory(_saveFolder);

            var files = new DirectoryInfo(_saveFolder)
                .GetFiles("*.json", SearchOption.TopDirectoryOnly);

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

            var result = new List<string>(files.Length);
            foreach (var f in files)
                result.Add(f.FullName);

            return result;
        }

        // ============================================================
        // Space / Object updates
        // ============================================================

        public Object AddSpace(string spaceName, string intent = null)
        {
            EnsureCurrent();
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");

            Current.Spaces ??= new List<Object>();

            var space = new Object
            {
                Name = spaceName,
                Intent = intent ?? "Space",
                Components = new List<Component>(),
                Parent = null,
                Children = new List<Object>()
            };

            Current.Spaces.Add(space);
            return space;
        }

        /// <summary>
        /// Add a new object under a given parent object (or as a root in a space if parentName is null).
        /// Returns the created object.
        /// </summary>
        public Object AddObject(string spaceName, string objectName, string intent = null, string parentName = null)
        {
            EnsureCurrent();

            var space = FindSpace(spaceName)
                        ?? throw new InvalidOperationException($"Space '{spaceName}' not found.");

            var newObj = new Object
            {
                Name = objectName,
                Intent = intent ?? string.Empty,
                Components = new List<Component>(),
                Children = new List<Object>()
            };

            if (string.IsNullOrWhiteSpace(parentName))
            {
                // Attach directly under the space root
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

        /// <summary>
        /// Remove an object by name from a given space. Removes entire subtree.
        /// </summary>
        public bool RemoveObject(string spaceName, string objectName)
        {
            EnsureCurrent();

            var space = FindSpace(spaceName);
            if (space == null) return false;

            // Can't remove the space root itself via this method
            if (string.Equals(space.Name, objectName, StringComparison.OrdinalIgnoreCase))
                return false;

            // Find parent + remove from parent's children
            var target = FindObjectInSpace(space, objectName);
            if (target == null) return false;

            var parent = target.Parent;
            if (parent?.Children == null) return false;

            return parent.Children.Remove(target);
        }

        // ============================================================
        // Component updates
        // ============================================================

        public Component AddComponent(string spaceName, string objectName, string componentType)
        {
            EnsureCurrent();

            var obj = RequireObject(spaceName, objectName);

            obj.Components ??= new List<Component>();

            var comp = new Component
            {
                Type = componentType,
                Assets = new List<string>(),
                Properties = new List<PropertyDesc>(),
                BehaviorScript = string.Empty
            };

            // build property map later as needed
            obj.Components.Add(comp);
            return comp;
        }

        public bool RemoveComponent(string spaceName, string objectName, string componentType)
        {
            EnsureCurrent();

            var obj = RequireObject(spaceName, objectName);
            if (obj.Components == null) return false;

            int idx = obj.Components.FindIndex(c =>
                c != null && string.Equals(c.Type, componentType, StringComparison.OrdinalIgnoreCase));

            if (idx < 0) return false;

            obj.Components.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Set/replace BehaviorScript on a component.
        /// </summary>
        public void SetBehaviorScript(string spaceName, string objectName, string componentType, string behaviorScript)
        {
            EnsureCurrent();

            var comp = RequireComponent(spaceName, objectName, componentType);
            comp.BehaviorScript = behaviorScript ?? string.Empty;
        }

        // ============================================================
        // Property updates
        // ============================================================

        /// <summary>
        /// Add a new property or replace existing property value by name.
        /// </summary>
        public void AddOrSetPropertyValue(
            string spaceName,
            string objectName,
            string componentType,
            string propertyName,
            string propertyType,
            JToken value)
        {
            EnsureCurrent();

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

            // If you keep a cache map inside Component, rebuild it when properties change
            comp.RebuildPropertyMapIfExists();
        }

        /// <summary>
        /// Convenience overload: accepts any CLR object and converts to JToken.
        /// </summary>
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

        public bool DeleteProperty(
            string spaceName,
            string objectName,
            string componentType,
            string propertyName)
        {
            EnsureCurrent();

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
        // Internal helpers
        // ============================================================

        private void EnsureCurrent()
        {
            if (Current == null)
                throw new InvalidOperationException("Current GameDesc is null. Call CreateNew(...) or LoadFromDisk(...) first.");
        }

        private Object FindSpace(string spaceName)
        {
            if (Current?.Spaces == null) return null;

            return Current.Spaces.Find(s =>
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

            // Depth-first search over Children
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
                // If Component has OnDeserialized / cache build, it will already run via Newtonsoft
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
        // File wrapper: keep ProjectPath with the GameDesc snapshot
        // ============================================================

        [Serializable]
        private class GameDescFile
        {
            public string ProjectPath;
            public GameDesc GameDesc;
        }
    }

    // ------------------------------------------------------------
    // Small extension to work with your current Component code
    // ------------------------------------------------------------
    public static class ComponentExtensions
    {
        /// <summary>
        /// If your Component has a property cache map, rebuild it.
        /// If it doesn't, this is a no-op.
        /// </summary>
        public static void RebuildPropertyMapIfExists(this Component c)
        {
            // If you have RebuildPropertyMap() method, call it here.
            // This keeps GameDescManager decoupled from your internal cache implementation.
            // Example:
            // c.RebuildPropertyMap();

            // If you don't want to expose a public rebuild method, you can remove calls to this extension.
        }
    }
}
