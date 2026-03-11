using System;
using UnityEngine;

namespace T2G
{
    public class Utils
    {
        public static bool IsPrimitiveDesc(string desc, out PrimitiveType? primitiveType)
        {
            foreach (PrimitiveType enumType in Enum.GetValues(typeof(PrimitiveType)))
            {
                string typeName = enumType.ToString();
                if (string.Compare(typeName, desc, true) == 0)
                {
                    primitiveType = enumType;
                    return true;
                }
            }
            primitiveType = null;
            return false;
        }

        public static bool IsObjectDesc(string desc)
        {
            return (string.Compare(desc.Trim(), "object", true) == 0);
        }
    }
}