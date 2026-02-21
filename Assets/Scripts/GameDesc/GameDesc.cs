using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [Serializable]
    public class GameDesc
    {
        public string Title;
        public List<T2G.Assistant.Object> Spaces;
    }

    [Serializable]
    public class Object
    {
        public string Name;
        public string Intent;
        public List<T2G.Assistant.Component> Components = new List<Component>();
        public T2G.Assistant.Object Parent;
        public List<T2G.Assistant.Object> Children = new List<Object>();
    }

    [Serializable]
    public class Component
    {
        public string Type;
        public List<string> Assets;
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public string BehaviorScript;

        // Runtime cache (fast lookup; rebuild when needed)
        [NonSerialized] 
        private Dictionary<string, PropertyDesc> _propertyMap = new Dictionary<string, PropertyDesc>();

        public bool TryGetProperty(string name, out PropertyDesc prop)
        {
            return _propertyMap.TryGetValue(name, out prop);
        }

        public PropertyDesc GetProperty(string name)
        {
            _propertyMap.TryGetValue(name, out var prop);
            return prop;
        }

        private void BuildPropertyMap()
        {
            _propertyMap.Clear();
            
            if (Properties == null || Properties.Count == 0)
            {
                return;
            }

            foreach (var p in Properties)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name))
                {
                    continue;
                }
                _propertyMap[p.Name] = p;
            }
        }

    }

    [Serializable]
    public class PropertyDesc
    {
        public string Name;
        public string Type;     //Keep this field for explicit validation, documentation, and training examples.
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
                    // Support RGB or RGBA
                    return IsArrayOfLength(prop.Value, 3)
                        || IsArrayOfLength(prop.Value, 4);

                case "ObjectRef":
                    // Keep simple: reference by string (name/path/guid)
                    // Avoid nested structures
                    // Keeps JSON generation simple for the LLM
                    return prop.Value.Type == JTokenType.String;
                default:
                    // Unknown type — allow flexible fallback
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

}