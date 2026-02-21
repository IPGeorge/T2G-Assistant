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
            _saveFolder = Path.Combine(Application.persistentDataPath, "GameDescs");
            if (!Directory.Exists(_saveFolder))
            {
                Directory.CreateDirectory(_saveFolder);
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

        /// <summary>
        /// Snapshot context (project path, last save path, timestamps).
        /// </summary>
        public SnapshotContext Context { get; private set; } = new SnapshotContext();

        // -------------------------
        // Internal
        // -------------------------
        private readonly string _saveFolder;
        private readonly JsonSerializerSettings _jsonSettings;

        // ============================================================
        // Domain: Snapshot lifecycle
        // ============================================================

        public GameDesc CreateGameDesc(string title, string projectPath)
        {
            Snapshot = new GameDesc
            {
                Title = title ?? "Untitled",
                Spaces = new List<Object>()
            };

            Context = new SnapshotContext
            {
                ProjectPath = projectPath ?? string.Empty,
                LastFilePath = string.Empty,
                CreatedUtc = DateTime.UtcNow,
                LastSavedUtc = null
            };

            return Snapshot;
        }

        public string SaveGameDesc(string fileName = null)
        {
            EnsureSnapshot();

            var wrapper = new GameDescFile
            {
                Context = Context,
                GameDesc = Snapshot
            };

            string json = JsonConvert.SerializeObject(wrapper, _jsonSettings);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                string safeTitle = MakeSafeFileName(Snapshot.Title);
                fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }
            else if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            string fullPath = Path.Combine(_saveFolder, fileName);
            File.WriteAllText(fullPath, json);

            Context.LastFilePath = fullPath;
            Context.LastSavedUtc = DateTime.UtcNow;

            return fullPath;
        }

        public GameDesc LoadGameDesc(string filePath)
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

            // Context is optional for backward compatibility with older saved files.
            Context = wrapper.Context ?? new SnapshotContext();
            Context.LastFilePath = filePath;

            // Rebuild parent pointers for hierarchy correctness.
            RebuildParents(Snapshot);

            // Normalize lists/caches.
            Normalize(Snapshot);

            return Snapshot;
        }

        public List<string> ListSavedGameDescs()
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
        // Domain: Snapshot edits
        // ============================================================

        public Object AddSpace(string spaceName, string intent = null)
        {
            EnsureSnapshot();
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");

            Snapshot.Spaces ??= new List<Object>();

            var space = new Object
            {
                Name = spaceName,
                Intent = intent ?? "Space",
                Components = new List<Component>(),
                Parent = null,
                Children = new List<Object>()
            };

            Snapshot.Spaces.Add(space);
            return space;
        }

        public Object AddObject(string spaceName, string objectName, string intent = null, string parentName = null)
        {
            EnsureSnapshot();

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
            public SnapshotContext Context;
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
}
