using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace T2G
{
    public static class InstructionParameterExtensions
    {
        public static bool TryGet(this List<Instruction.Parameter> list, string name, out JToken value)
        {
            if (list != null)
            {
                foreach (var p in list)
                {
                    if (p != null && string.Equals(p.name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        value = p.value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        // Models occasionally send an array/object where a scalar is expected
        // (for example a Vector3 value for a "String" parameter). Never cast
        // those blindly: JToken.Value<T>() throws InvalidCastException.
        public static string GetString(this List<Instruction.Parameter> list, string name, string fallback = "")
        {
            if (!list.TryGet(name, out var v) || v == null) return fallback;
            if (v.Type == JTokenType.Array || v.Type == JTokenType.Object) return fallback;
            return v.Value<string>() ?? fallback;
        }

        public static bool GetBool(this List<Instruction.Parameter> list, string name, bool fallback = false)
        {
            if (!list.TryGet(name, out var v) || v == null) return fallback;
            if (v.Type == JTokenType.Boolean) return v.Value<bool>();
            if (v.Type == JTokenType.String && bool.TryParse(v.Value<string>(), out var parsed)) return parsed;
            return fallback;
        }

        public static int GetInt(this List<Instruction.Parameter> list, string name, int fallback = 0)
        {
            if (!list.TryGet(name, out var v) || v == null) return fallback;
            if (v.Type == JTokenType.Integer) return v.Value<int>();
            if (v.Type == JTokenType.Float) return (int)v.Value<double>();
            if (v.Type == JTokenType.String && int.TryParse(v.Value<string>(), out var parsed)) return parsed;
            return fallback;
        }

        /// <summary>
        /// Reads a Vector3 parameter declared either as an array
        /// ([x, y, z]) or as a string ("(x, y, z)", "x,y,z", "[x, y, z]").
        /// Returns false when the parameter is missing or unparseable.
        /// </summary>
        public static bool TryGetVector3(this List<Instruction.Parameter> list, string name, out Vector3 value)
        {
            value = default;

            if (!list.TryGet(name, out var v) || v == null) return false;

            if (v.Type == JTokenType.Array)
            {
                var arr = (JArray)v;
                if (arr.Count < 3) return false;

                try
                {
                    value = new Vector3(
                        arr[0].Value<float>(),
                        arr[1].Value<float>(),
                        arr[2].Value<float>());
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (v.Type == JTokenType.String)
            {
                Vector3? parsed = Utils.ParseVector3(v.Value<string>());
                if (parsed.HasValue)
                {
                    value = parsed.Value;
                    return true;
                }
            }

            return false;
        }

        public static Vector3 GetVector3(this List<Instruction.Parameter> list, string name, Vector3 fallback)
            => list.TryGetVector3(name, out var value) ? value : fallback;

        public static JToken GetValue(this List<Instruction.Parameter> list, string name)
        {
            return list.TryGet(name, out var v) ? v : null;
        }
    }
   
    /*
    public static class InstructionValuePairExtensions
    {
        public static bool TryGet(this List<ValuePair> list, string name, out JToken value)
        {
            if (list != null)
            {
                foreach (var p in list)
                {
                    if (p != null && string.Equals(p.name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        value = p.value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        public static string GetString(this List<ValuePair> list, string name, string fallback = "")
            => list.TryGet(name, out var v) ? v.Value<string>() : fallback;

        public static bool GetBool(this List<ValuePair> list, string name, bool fallback = false)
            => list.TryGet(name, out var v) ? v.Value<bool>() : fallback;

        public static int GetInt(this List<ValuePair> list, string name, int fallback = 0)
            => list.TryGet(name, out var v) ? v.Value<int>() : fallback;

        public static Vector3 GetVector3(this List<ValuePair> list, string name, Vector3 fallback)
        {
            if (!list.TryGet(name, out var v)) return fallback;
            if (v is JArray arr && arr.Count == 3)
            {
                return new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
            }
            return fallback;
        }

        public static JToken GetValue(this List<ValuePair> list, string name)
        {
            return list.TryGet(name, out var v) ? v : null;
        }
    }
    */
}