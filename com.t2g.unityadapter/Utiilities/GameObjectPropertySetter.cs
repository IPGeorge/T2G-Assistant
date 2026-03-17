using UnityEngine;
using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

public static class GameObjectPropertySetter
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
    private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> _fieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();

    /// <summary>
    /// Sets a property on a GameObject with explicit parameters
    /// </summary>
    public static bool SetProperty(GameObject target, string propertyName, string valueStr, out string resultMessage)
    {
        resultMessage = "";

        try
        {
            // First, check if it's a property on the GameObject itself
            if (TrySetPropertyOnObject(target, propertyName, valueStr, out resultMessage))
            {
                return true;
            }

            // If not found, search through all components
            Component[] components = target.GetComponents<Component>();

            // Sort components by relevance (Transform first, then Renderer, then others)
            var sortedComponents = components
                .Where(c => c != null)
                .OrderBy(c => GetComponentPriority(c.GetType()))
                .ToList();

            foreach (Component component in sortedComponents)
            {
                if (TrySetPropertyOnObject(component, propertyName, valueStr, out resultMessage))
                {
                    resultMessage = $"{component.GetType().Name}.{propertyName} was set to {FormatValue(valueStr)}";
                    return true;
                }
            }

            // Try nested properties (like transform.position.x)
            if (propertyName.Contains('.'))
            {
                string[] parts = propertyName.Split('.');
                string mainProperty = parts[0];
                string subProperty = string.Join(".", parts.Skip(1));

                // Try to find the object that has the main property
                if (TryGetPropertyOwner(target, mainProperty, out object owner, out MemberInfo member))
                {
                    if (TrySetPropertyOnObject(owner, subProperty, valueStr, out resultMessage))
                    {
                        string ownerName = owner is Component ? (owner as Component).GetType().Name : "GameObject";
                        resultMessage = $"{ownerName}.{propertyName} was set to {FormatValue(valueStr)}";
                        return true;
                    }
                }
            }

            resultMessage = $"Could not find property '{propertyName}' on GameObject '{target.name}' or any of its components";
            return false;
        }
        catch (Exception e)
        {
            resultMessage = $"Error setting property: {e.Message}";
            return false;
        }
    }

    private static int GetComponentPriority(Type type)
    {
        if (type == typeof(Transform)) return 0;
        if (type == typeof(RectTransform)) return 1;
        if (type.Name.Contains("Renderer")) return 2;
        if (type == typeof(Camera)) return 3;
        if (type == typeof(Light)) return 4;
        if (type == typeof(Canvas)) return 5;
        if (type == typeof(Rigidbody)) return 6;
        if (type == typeof(Collider)) return 7;
        return 10;
    }

    private static bool TryGetPropertyOwner(GameObject target, string propertyName, out object owner, out MemberInfo memberInfo)
    {
        owner = null;
        memberInfo = null;

        // Check GameObject first
        if (TryGetMember(target, propertyName, out memberInfo))
        {
            owner = target;
            return true;
        }

        // Check components
        Component[] components = target.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && TryGetMember(component, propertyName, out memberInfo))
            {
                owner = component;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMember(object obj, string memberName, out MemberInfo memberInfo)
    {
        memberInfo = null;
        Type type = obj.GetType();

        // Check properties
        var prop = GetCachedProperty(type, memberName);
        if (prop != null)
        {
            memberInfo = prop;
            return true;
        }

        // Check fields
        var field = GetCachedField(type, memberName);
        if (field != null)
        {
            memberInfo = field;
            return true;
        }

        return false;
    }

    private static bool TrySetPropertyOnObject(object obj, string propertyName, string valueStr, out string resultMessage)
    {
        resultMessage = "";

        Type type = obj.GetType();

        // Try as property
        PropertyInfo property = GetCachedProperty(type, propertyName);
        if (property != null && property.CanWrite)
        {
            object convertedValue = ConvertValue(valueStr, property.PropertyType);
            property.SetValue(obj, convertedValue);
            resultMessage = $"'{propertyName}' was set to {FormatValue(valueStr)}";
            return true;
        }

        // Try as field
        FieldInfo field = GetCachedField(type, propertyName);
        if (field != null)
        {
            object convertedValue = ConvertValue(valueStr, field.FieldType);
            field.SetValue(obj, convertedValue);
            resultMessage = $"'{propertyName}' was set to {FormatValue(valueStr)}";
            return true;
        }

        return false;
    }

    private static PropertyInfo GetCachedProperty(Type type, string propertyName)
    {
        if (!_propertyCache.ContainsKey(type))
        {
            _propertyCache[type] = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            // Cache all properties
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in properties)
            {
                if (!_propertyCache[type].ContainsKey(prop.Name))
                {
                    _propertyCache[type][prop.Name] = prop;
                }

                // Also cache common aliases
                string alias = GetPropertyAlias(prop.Name);
                if (alias != null && !_propertyCache[type].ContainsKey(alias))
                {
                    _propertyCache[type][alias] = prop;
                }
            }
        }

        return _propertyCache[type].TryGetValue(propertyName, out PropertyInfo propInfo) ? propInfo : null;
    }

    private static FieldInfo GetCachedField(Type type, string fieldName)
    {
        if (!_fieldCache.ContainsKey(type))
        {
            _fieldCache[type] = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                _fieldCache[type][field.Name] = field;
            }
        }

        return _fieldCache[type].TryGetValue(fieldName, out FieldInfo fieldInfo) ? fieldInfo : null;
    }

    private static string GetPropertyAlias(string propertyName)
    {
        switch (propertyName.ToLower())
        {
            case "position": return "pos";
            case "localposition": return "localPos";
            case "rotation": return "rot";
            case "localrotation": return "localRot";
            case "localscale": return "scale";
            case "eulerangles": return "euler";
            case "localeulerangles": return "localEuler";
            default: return null;
        }
    }

    private static object ConvertValue(string valueStr, Type targetType)
    {
        valueStr = valueStr.Trim();

        // Handle null
        if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            valueStr.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Boolean
        if (targetType == typeof(bool))
        {
            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (valueStr.Equals("1")) return true;
            if (valueStr.Equals("0")) return false;
            if (valueStr.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
            if (valueStr.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            return bool.Parse(valueStr);
        }

        // String
        if (targetType == typeof(string))
        {
            // Remove quotes if present
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                return valueStr.Substring(1, valueStr.Length - 2);
            if (valueStr.StartsWith("'") && valueStr.EndsWith("'"))
                return valueStr.Substring(1, valueStr.Length - 2);
            return valueStr;
        }

        // Integer types
        if (targetType == typeof(int)) return int.Parse(valueStr);
        if (targetType == typeof(uint)) return uint.Parse(valueStr);
        if (targetType == typeof(short)) return short.Parse(valueStr);
        if (targetType == typeof(ushort)) return ushort.Parse(valueStr);
        if (targetType == typeof(long)) return long.Parse(valueStr);
        if (targetType == typeof(ulong)) return ulong.Parse(valueStr);
        if (targetType == typeof(byte)) return byte.Parse(valueStr);
        if (targetType == typeof(sbyte)) return sbyte.Parse(valueStr);

        // Float types
        if (targetType == typeof(float)) return float.Parse(valueStr, CultureInfo.InvariantCulture);
        if (targetType == typeof(double)) return double.Parse(valueStr, CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal)) return decimal.Parse(valueStr, CultureInfo.InvariantCulture);

        // Vector2
        if (targetType == typeof(Vector2))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 2);
            return new Vector2(values[0], values[1]);
        }

        // Vector3
        if (targetType == typeof(Vector3))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 3);
            return new Vector3(values[0], values[1], values[2]);
        }

        // Vector4
        if (targetType == typeof(Vector4))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 4);
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        // Vector2Int
        if (targetType == typeof(Vector2Int))
        {
            int[] values = ParseParenthesizedInts(valueStr, 2);
            return new Vector2Int(values[0], values[1]);
        }

        // Vector3Int
        if (targetType == typeof(Vector3Int))
        {
            int[] values = ParseParenthesizedInts(valueStr, 3);
            return new Vector3Int(values[0], values[1], values[2]);
        }

        // Color
        if (targetType == typeof(Color))
        {
            // Try hex format (#RRGGBB or #RRGGBBAA)
            if (valueStr.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(valueStr, out Color color))
                    return color;
            }

            // Try RGB/RGBA format (r,g,b) or (r,g,b,a)
            float[] values = ParseParenthesizedNumbers(valueStr, 3, 4);
            if (values.Length == 3)
                return new Color(values[0], values[1], values[2]);
            if (values.Length == 4)
                return new Color(values[0], values[1], values[2], values[3]);
        }

        // Rect
        if (targetType == typeof(Rect))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 4);
            return new Rect(values[0], values[1], values[2], values[3]);
        }

        // Bounds
        if (targetType == typeof(Bounds))
        {
            string[] parts = valueStr.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                Vector3 center = ParseVector3(parts[0].Trim());
                Vector3 size = ParseVector3(parts[1].Trim());
                return new Bounds(center, size);
            }
        }

        // Quaternion (as euler angles)
        if (targetType == typeof(Quaternion))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 3);
            return Quaternion.Euler(values[0], values[1], values[2]);
        }

        // Enum
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, valueStr, true);
        }

        // Try Convert.ChangeType as fallback
        return Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture);
    }

    private static float[] ParseParenthesizedNumbers(string valueStr, int minLength, int maxLength = -1)
    {
        if (maxLength == -1) maxLength = minLength;

        // Remove parentheses if present
        string cleaned = valueStr.Trim();
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")"))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

        // Split by common separators
        string[] parts = cleaned.Split(new[] { ',', ' ', ';', '|' },
            StringSplitOptions.RemoveEmptyEntries);

        float[] result = new float[Math.Min(parts.Length, maxLength)];
        for (int i = 0; i < result.Length && i < parts.Length; i++)
        {
            if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                result[i] = value;
            }
            else
            {
                throw new FormatException($"Invalid number format: {parts[i]}");
            }
        }

        if (result.Length < minLength)
        {
            throw new FormatException($"Expected at least {minLength} numbers, got {result.Length}");
        }

        return result;
    }

    private static int[] ParseParenthesizedInts(string valueStr, int minLength, int maxLength = -1)
    {
        if (maxLength == -1) maxLength = minLength;

        string cleaned = valueStr.Trim();
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")"))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

        string[] parts = cleaned.Split(new[] { ',', ' ', ';', '|' },
            StringSplitOptions.RemoveEmptyEntries);

        int[] result = new int[Math.Min(parts.Length, maxLength)];
        for (int i = 0; i < result.Length && i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int value))
            {
                result[i] = value;
            }
            else
            {
                throw new FormatException($"Invalid integer format: {parts[i]}");
            }
        }

        if (result.Length < minLength)
        {
            throw new FormatException($"Expected at least {minLength} integers, got {result.Length}");
        }

        return result;
    }

    private static Vector3 ParseVector3(string str)
    {
        float[] values = ParseParenthesizedNumbers(str, 3);
        return new Vector3(values[0], values[1], values[2]);
    }

    private static string FormatValue(string valueStr)
    {
        // Just return the original for display purposes
        return valueStr;
    }

    private static GameObject FindGameObject(string name)
    {
        // First try GameObject.Find (finds active objects only)
        GameObject obj = GameObject.Find(name);
        if (obj != null) return obj;

        // If not found, search all objects including inactive
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name == name && go.scene.isLoaded) // Make sure it's in a loaded scene
            {
                return go;
            }
        }

        return null;
    }
}