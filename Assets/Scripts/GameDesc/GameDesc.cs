using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace T2G.Assistant
{
    [Serializable]
    public class GameDesc
    {
        public string ProjectName = string.Empty;
        public string Title = string.Empty;
        public List<Space> Spaces = new List<Space>();
        public List<InstructionRecord> InstructionHistory = new List<InstructionRecord>();
    }

    [Serializable]
    public class InstructionRecord
    {
        public string InstructionJson = string.Empty;
        public string ResponseJson = string.Empty;
        public DateTime ExecutedUtc;
        public bool Succeeded;
    }

    [Serializable]
    public class Space
    {
        public string Id = string.Empty;
        public string Name = string.Empty;

        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<Component> Components = new List<Component>();

        public Dictionary<string, Object> Objects =
            new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, GameAsset> Assets =
            new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);

        [NonSerialized]
        private Dictionary<string, string> _nameToId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [NonSerialized]
        private Dictionary<string, PropertyDesc> _propertyMap =
            new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

        public void RebuildNameIndex()
        {
            _nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (Objects == null)
                return;

            foreach (var pair in Objects)
            {
                Object obj = pair.Value;
                if (obj == null || string.IsNullOrWhiteSpace(obj.Name))
                    continue;

                _nameToId[obj.Name] = pair.Key;
            }
        }

        public bool TryGetObjectByName(string name, out Object obj)
        {
            obj = null;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (_nameToId == null || _nameToId.Count == 0)
                RebuildNameIndex();

            string id;
            if (!_nameToId.TryGetValue(name, out id))
                return false;

            return Objects != null && Objects.TryGetValue(id, out obj);
        }

        public bool TryGetObjectById(string id, out Object obj)
        {
            obj = null;
            return !string.IsNullOrWhiteSpace(id) &&
                   Objects != null &&
                   Objects.TryGetValue(id, out obj);
        }

        public bool TryGetProperty(string name, out PropertyDesc property)
        {
            EnsurePropertyMap();
            return _propertyMap.TryGetValue(name, out property);
        }

        public PropertyDesc GetPropertyOrNull(string name)
        {
            PropertyDesc property;
            return TryGetProperty(name, out property) ? property : null;
        }

        public void RebuildPropertyMap()
        {
            BuildPropertyMap();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Properties = Properties ?? new List<PropertyDesc>();
            Components = Components ?? new List<Component>();
            Objects = Objects ?? new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
            Assets = Assets ?? new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);

            RebuildNameIndex();
            BuildPropertyMap();
        }

        private void EnsurePropertyMap()
        {
            if (_propertyMap == null)
                BuildPropertyMap();
        }

        private void BuildPropertyMap()
        {
            _propertyMap = new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

            if (Properties == null)
                return;

            foreach (PropertyDesc property in Properties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                    continue;

                _propertyMap[property.Name] = property;
            }
        }
    }

    [Serializable]
    public class Object
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Desc = string.Empty;

        public List<string> Tags = new List<string>();
        public List<string> Roles = new List<string>();
        public List<Relationship> Relationships = new List<Relationship>();
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<AssetRef> Assets = new List<AssetRef>();
        public List<Component> Components = new List<Component>();

        [NonSerialized]
        private Dictionary<string, PropertyDesc> _propertyMap =
            new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

        public bool TryGetProperty(string name, out PropertyDesc property)
        {
            EnsurePropertyMap();
            return _propertyMap.TryGetValue(name, out property);
        }

        public PropertyDesc GetPropertyOrNull(string name)
        {
            PropertyDesc property;
            return TryGetProperty(name, out property) ? property : null;
        }

        public void RebuildPropertyMap()
        {
            BuildPropertyMap();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Tags = Tags ?? new List<string>();
            Roles = Roles ?? new List<string>();
            Relationships = Relationships ?? new List<Relationship>();
            Properties = Properties ?? new List<PropertyDesc>();
            Assets = Assets ?? new List<AssetRef>();
            Components = Components ?? new List<Component>();

            BuildPropertyMap();
        }

        private void EnsurePropertyMap()
        {
            if (_propertyMap == null)
                BuildPropertyMap();
        }

        private void BuildPropertyMap()
        {
            _propertyMap = new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

            if (Properties == null)
                return;

            foreach (PropertyDesc property in Properties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                    continue;

                _propertyMap[property.Name] = property;
            }
        }
    }

    [Serializable]
    public class Relationship
    {
        public string Type = string.Empty;
        public string Target = string.Empty; // Stable target object GUID.
        public string Slot = string.Empty;
    }

    public enum ComponentSourceType
    {
        Unknown = 0,
        Engine = 1,
        Script = 2,
        T2G = 3
    }

    [Serializable]
    public class Component
    {
        public string Type = string.Empty;
        public ComponentSourceType SourceType = ComponentSourceType.Unknown;
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<AssetRef> Assets = new List<AssetRef>();
        public string Description = string.Empty;

        [NonSerialized]
        private Dictionary<string, PropertyDesc> _propertyMap =
            new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

        public bool TryGetProperty(string name, out PropertyDesc property)
        {
            EnsurePropertyMap();
            return _propertyMap.TryGetValue(name, out property);
        }

        public PropertyDesc GetPropertyOrNull(string name)
        {
            PropertyDesc property;
            return TryGetProperty(name, out property) ? property : null;
        }

        public void RebuildPropertyMap()
        {
            BuildPropertyMap();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Properties = Properties ?? new List<PropertyDesc>();
            Assets = Assets ?? new List<AssetRef>();
            BuildPropertyMap();
        }

        private void EnsurePropertyMap()
        {
            if (_propertyMap == null)
                BuildPropertyMap();
        }

        private void BuildPropertyMap()
        {
            _propertyMap = new Dictionary<string, PropertyDesc>(StringComparer.OrdinalIgnoreCase);

            if (Properties == null)
                return;

            foreach (PropertyDesc property in Properties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                    continue;

                _propertyMap[property.Name] = property;
            }
        }
    }

    [Serializable]
    public class GameAsset
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Source = string.Empty;
        public string ImportPath = string.Empty;
        public string LoadPath = string.Empty;
        public string Type = string.Empty;
    }

    [Serializable]
    public class AssetRef
    {
        public string AssetId = string.Empty;
    }

    [Serializable]
    public class PropertyDesc
    {
        public string Name = string.Empty;
        public string Type = string.Empty;
        public JToken Value;

        public static bool ValidateType(PropertyDesc property)
        {
            if (property == null || property.Value == null || string.IsNullOrWhiteSpace(property.Type))
                return false;

            switch (property.Type.ToLowerInvariant())
            {
                case "bool":
                case "boolean":
                    return property.Value.Type == JTokenType.Boolean;

                case "int":
                case "integer":
                    return property.Value.Type == JTokenType.Integer;

                case "float":
                case "double":
                    return property.Value.Type == JTokenType.Float ||
                           property.Value.Type == JTokenType.Integer;

                case "string":
                case "objectref":
                case "spaceref":
                case "assetref":
                    return property.Value.Type == JTokenType.String;

                case "vector2":
                    return IsNumericArrayOfLength(property.Value, 2);

                case "vector3":
                    return IsNumericArrayOfLength(property.Value, 3);

                case "vector4":
                    return IsNumericArrayOfLength(property.Value, 4);

                case "color":
                    return IsNumericArrayOfLength(property.Value, 3) ||
                           IsNumericArrayOfLength(property.Value, 4);

                default:
                    // Custom/engine-specific types may be validated later.
                    return true;
            }
        }

        private static bool IsNumericArrayOfLength(JToken token, int length)
        {
            JArray array = token as JArray;
            if (array == null || array.Count != length)
                return false;

            foreach (JToken item in array)
            {
                if (item.Type != JTokenType.Integer &&
                    item.Type != JTokenType.Float)
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
