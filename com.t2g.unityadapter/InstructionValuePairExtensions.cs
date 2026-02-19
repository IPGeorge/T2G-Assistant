using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace T2G
{
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
    }

}