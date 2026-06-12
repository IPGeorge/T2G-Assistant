using UnityEngine;
using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using T2G;

public static class GameObjectPropertySetter
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
    private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> _fieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();

    public static bool SetProperty(GameObject target, string propertyName, string valueStr, out string resultMessage)
    {
        resultMessage = "";

        try
        {
            if (propertyName.Contains('.'))
            {
                string[] parts = propertyName.Split('.', 2);
                string componentTypeName = parts[0];
                string propertyOnlyName = parts[1];

                var componentType = ComponentResolver.GetComponentType(componentTypeName);
                if (componentType != null)
                {
                    var component = target.GetComponent(componentType);
                    if (component == null)
                    {
                        resultMessage = $"Component '{componentTypeName}' not found on GameObject '{target.name}'.";
                        return false;
                    }

                    if (TrySetPropertyOnObject(component, propertyOnlyName, valueStr, out resultMessage))
                    {
                        resultMessage = $"{componentTypeName}.{propertyOnlyName} was set to {FormatValue(valueStr)}";
                        Utils.UpdateEditorViews();
                        return true;
                    }

                    resultMessage = $"Property '{propertyOnlyName}' not found on component '{componentTypeName}'.";
                    return false;
                }
            }

            if (TrySetPropertyOnObject(target, propertyName, valueStr, out resultMessage))
            {
                Utils.UpdateEditorViews();
                return true;
            }

            Component[] components = target.GetComponents<Component>();

            var sortedComponents = components
                .Where(c => c != null)
                .OrderBy(c => GetComponentPriority(c.GetType()))
                .ToList();

            foreach (Component component in sortedComponents)
            {
                if (TrySetPropertyOnObject(component, propertyName, valueStr, out resultMessage))
                {
                    resultMessage = $"{target.name} {component.GetType().Name}.{propertyName} was set to {FormatValue(valueStr)}";
                    Utils.UpdateEditorViews();
                    return true;
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



    private static bool TrySetPropertyOnObject(object obj, string propertyName, string valueStr, out string resultMessage)
    {
        Type type;
        resultMessage = "";

        if (propertyName.Contains('.'))
        {
            string[] parts = propertyName.Split(new[] { '.' }, 2);
            string firstProperty = parts[0];
            string remainingProperties = parts[1];

            type = obj.GetType();

            PropertyInfo property = GetCachedProperty(type, firstProperty);
            if (property != null)
            {
                object propertyValue = property.GetValue(obj);
                if (propertyValue == null)
                {
                    resultMessage = $"Property '{firstProperty}' is null on {type.Name}";
                    return false;
                }
                return TrySetPropertyOnObject(propertyValue, remainingProperties, valueStr, out resultMessage);
            }

            FieldInfo field = GetCachedField(type, firstProperty);
            if (field != null)
            {
                object fieldValue = field.GetValue(obj);
                if (fieldValue == null)
                {
                    resultMessage = $"Field '{firstProperty}' is null on {type.Name}";
                    return false;
                }
                return TrySetPropertyOnObject(fieldValue, remainingProperties, valueStr, out resultMessage);
            }

            return false;
        }

        type = obj.GetType();

        PropertyInfo directProperty = GetCachedProperty(type, propertyName);
        if (directProperty != null && directProperty.CanWrite)
        {
            object convertedValue = ConvertValue(valueStr, directProperty.PropertyType);
            directProperty.SetValue(obj, convertedValue);
            resultMessage = $"'{propertyName}' was set to {FormatValue(valueStr)}";
            return true;
        }

        FieldInfo directField = GetCachedField(type, propertyName);
        if (directField != null)
        {
            object convertedValue = ConvertValue(valueStr, directField.FieldType);
            directField.SetValue(obj, convertedValue);
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

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in properties)
            {
                if (!_propertyCache[type].ContainsKey(prop.Name))
                {
                    _propertyCache[type][prop.Name] = prop;
                }

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

        if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            valueStr.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

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

        if (targetType == typeof(string))
        {
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                return valueStr.Substring(1, valueStr.Length - 2);
            if (valueStr.StartsWith("'") && valueStr.EndsWith("'"))
                return valueStr.Substring(1, valueStr.Length - 2);
            return valueStr;
        }

        if (targetType == typeof(int)) return int.Parse(valueStr);
        if (targetType == typeof(uint)) return uint.Parse(valueStr);
        if (targetType == typeof(short)) return short.Parse(valueStr);
        if (targetType == typeof(ushort)) return ushort.Parse(valueStr);
        if (targetType == typeof(long)) return long.Parse(valueStr);
        if (targetType == typeof(ulong)) return ulong.Parse(valueStr);
        if (targetType == typeof(byte)) return byte.Parse(valueStr);
        if (targetType == typeof(sbyte)) return sbyte.Parse(valueStr);

        if (targetType == typeof(float)) return float.Parse(valueStr, CultureInfo.InvariantCulture);
        if (targetType == typeof(double)) return double.Parse(valueStr, CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal)) return decimal.Parse(valueStr, CultureInfo.InvariantCulture);

        if (targetType == typeof(Vector2))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 2);
            return new Vector2(values[0], values[1]);
        }

        if (targetType == typeof(Vector3))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 3);
            return new Vector3(values[0], values[1], values[2]);
        }

        if (targetType == typeof(Vector4))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 4);
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        if (targetType == typeof(Vector2Int))
        {
            int[] values = ParseParenthesizedInts(valueStr, 2);
            return new Vector2Int(values[0], values[1]);
        }

        if (targetType == typeof(Vector3Int))
        {
            int[] values = ParseParenthesizedInts(valueStr, 3);
            return new Vector3Int(values[0], values[1], values[2]);
        }

        if (targetType == typeof(Color))
        {
            if (valueStr.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(valueStr, out Color color))
                    return color;
            }

            float[] values = ParseParenthesizedNumbers(valueStr, 3, 4);
            if (values.Length == 3)
                return new Color(values[0], values[1], values[2]);
            if (values.Length == 4)
                return new Color(values[0], values[1], values[2], values[3]);
        }

        if (targetType == typeof(Rect))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 4);
            return new Rect(values[0], values[1], values[2], values[3]);
        }

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

        if (targetType == typeof(Quaternion))
        {
            float[] values = ParseParenthesizedNumbers(valueStr, 3);
            return Quaternion.Euler(values[0], values[1], values[2]);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, valueStr, true);
        }

        return Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture);
    }

    private static float[] ParseParenthesizedNumbers(string valueStr, int minLength, int maxLength = -1)
    {
        if (maxLength == -1) maxLength = minLength;

        string cleaned = valueStr.Trim();
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")"))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

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
        return valueStr;
    }

    private static GameObject FindGameObject(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj != null) return obj;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name == name && go.scene.isLoaded)
            {
                return go;
            }
        }

        return null;
    }
}