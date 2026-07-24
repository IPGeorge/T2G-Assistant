using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace T2G.Assistant
{
    [Serializable]
    public class GameDesc
    {
        public string ProjectName;
        public string Title;
        public List<T2G.Assistant.Space> Spaces;
        public List<InstructionRecord> InstructionHistory;
    }

    [Serializable]
    public class InstructionRecord
    {
        public string InstructionJson;
        public DateTime ExecutedUtc;
        public bool Succeeded;
    }

    [Serializable]
    public class Space
    {
        public string Name;
        public List<T2G.Assistant.Component> Components = new List<Component>();

        /// <summary>
        /// Objects keyed by their GUID Id for O(1) lookup.
        /// </summary>
        public Dictionary<string, T2G.Assistant.Object> Objects = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Name → GUID index for resolving instructions (which use names) to internal IDs.
        /// Rebuilt by RebuildNameIndex() after any mutation or deserialization.
        /// </summary>
        [NonSerialized]
        public Dictionary<string, string> _nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Assets referenced by objects in this space.
        /// Key = download URL or absolute source path.
        /// Value = resolved load info (project-relative path + type hint).
        /// </summary>
        public Dictionary<string, AssetInfo> Assets = new Dictionary<string, AssetInfo>();

        public void RebuildNameIndex()
        {
            _nameToId.Clear();
            if (Objects == null) return;
            foreach (var kvp in Objects)
            {
                if (kvp.Value != null && !string.IsNullOrWhiteSpace(kvp.Value.Name))
                    _nameToId[kvp.Value.Name] = kvp.Key;
            }
        }
    }

    [Serializable]
    public class Object
    {
        public string Id;
        public string Name;
        public string Desc;
        public List<string> Tags = new List<string>();
        public List<string> Roles = new List<string>();
        public List<Relationship> Relationships = new List<Relationship>();
        public List<ValuePair> Properties = new List<ValuePair>();
        /// <summary>
        /// Keys into Space.Assets dictionary. Retrieve the full AssetInfo via space.Assets[key].
        /// Legacy format "url/path,LoadPath" is migrated during Normalize / MigrateFromLegacy.
        /// </summary>
        public List<string> Assets = new List<string>();
        public List<T2G.Assistant.Component> Components = new List<Component>();
    }

    /// <summary>
    /// Represents a named connection between two objects.
    /// Type defines the kind of relationship (e.g. "attached_to", "contains").
    /// Target is the GUID of the target object (not the display name).
    /// Slot is an optional named connection point on the target — for example,
    /// the socket name when Type is "attached_to", or an inventory slot when
    /// used with container-type relationships.
    /// </summary>
    [Serializable]
    public class Relationship
    {
        public string Type;
        public string Target;
        public string Slot;
    }

    [Serializable]
    public class Component
    {
        public string Type;
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<string> Assets = new List<string>();
        public string Description;
        public string BehaviorScript;

        // Snapshot cache (fast lookup). Not serialized.
        [NonSerialized]
        private Dictionary<string, PropertyDesc> _propertyMap;

        public bool TryGetProperty(string name, out PropertyDesc prop)
        {
            EnsurePropertyMap();
            return _propertyMap.TryGetValue(name, out prop);
        }

        public PropertyDesc GetPropertyOrNull(string name)
        {
            EnsurePropertyMap();
            _propertyMap.TryGetValue(name, out var prop);
            return prop;
        }

        /// <summary>
        /// Call this if you modify Properties after deserialization.
        /// </summary>
        public void RebuildPropertyMap()
        {
            BuildPropertyMap();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            BuildPropertyMap();
        }

        private void EnsurePropertyMap()
        {
            _propertyMap ??= new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);
        }

        private void BuildPropertyMap()
        {
            EnsurePropertyMap();
            _propertyMap.Clear();

            if (Properties == null || Properties.Count == 0)
                return;

            foreach (var p in Properties)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name))
                    continue;

                // last one wins if duplicates
                _propertyMap[p.Name] = p;
            }
        }
    }

    public static class ComponentExtensions
    {
        public static void RebuildPropertyMapIfExists(this Component c)
        {
            if (c == null) return;
            c.RebuildPropertyMap();
        }
    }

    [Serializable]
    public class AssetInfo
    {
        /// <summary>
        /// Unity project-relative path used to load the asset (e.g. "Assets/Models/chair.fbx").
        /// </summary>
        public string LoadPath;

        /// <summary>
        /// Asset type hint: prefab, model, texture, sprite, audio, package, script, etc.
        /// </summary>
        public string Type;
    }

    [Serializable]
    public class PropertyDesc
    {
        public string Name;
        public string Type;
        public JToken Value;    //Examples: Value = JToken.FromObject(1.0f);
                                //          Value = JToken.FromObject(new float[] { 0,0,0 });
                                //          Value = JToken.FromObject(true);
                                //          Value = JToken.FromObject(new Color( 1, 1, 1, 1));
                                //          var pos = Value.ToObject<Vector3>();  

        public static bool ValidateType(PropertyDesc prop)
        {
            if (prop == null || prop.Value == null || string.IsNullOrWhiteSpace(prop.Type))
                return false;

            switch (prop.Type)
            {
                case "bool":
                    return prop.Value.Type == JTokenType.Boolean;

                case "int":
                    return prop.Value.Type == JTokenType.Integer;

                case "float":
                    return prop.Value.Type == JTokenType.Float
                        || prop.Value.Type == JTokenType.Integer;

                case "string":
                    return prop.Value.Type == JTokenType.String;

                case "Vector2":
                    return IsArrayOfLength(prop.Value, 2);

                case "Vector3":
                    return IsArrayOfLength(prop.Value, 3);

                case "Vector4":
                    return IsArrayOfLength(prop.Value, 4);

                case "Color":
                    return IsArrayOfLength(prop.Value, 3)
                        || IsArrayOfLength(prop.Value, 4);

                case "ObjectRef":
                    return prop.Value.Type == JTokenType.String;
                default:
                    return true;
            }
        }

        private static bool IsArrayOfLength(JToken token, int length)
        {
            if (token is not JArray arr) return false;
            if (arr.Count != length) return false;

            foreach (var item in arr)
            {
                if (item.Type != JTokenType.Float &&
                    item.Type != JTokenType.Integer)
                    return false;
            }
            return true;
        }
    }

    public static class GameDescTags
    {
        public const string ThirdPerson = "third_person";
        public const string Interactive = "interactive";
        public const string Environment = "environment";
        public const string ShootingRange = "shooting_range";
        public const string Spawnable = "spawnable";
        public const string Decorative = "decorative";
        public const string MainCamera = "main_camera";
    }

    public static class GameDescRoles
    {
        public const string PlayerViewCamera = "player_view_camera";
        public const string PrimaryWeapon = "primary_weapon";
        public const string Projectile = "projectile";
        public const string Target = "target";
        public const string TargetSpawner = "target_spawner";
        public const string GameUI = "game_ui";
    }

    public static class GameDescRelationTypes
    {
        public const string AttachedTo = "attached_to";
        public const string Contains = "contains";
        public const string ProvidesViewFor = "provides_view_for";
        public const string EquippedBy = "equipped_by";
        public const string SpawnedBy = "spawned_by";
        public const string Controls = "controls";
        public const string Targets = "targets";
        public const string UsesAmmunitionFrom = "uses_ammunition_from";
    }
}
