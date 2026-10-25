using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace T2G.Assistant
{
    /// <summary>
    /// Keeps a persisted GameDesc property's declared <c>Type</c> and its
    /// <c>JToken</c> <c>Value</c> consistent.
    ///
    /// Execution responses report transforms as arrays (a Vector3 result is
    /// serialized as float[]), while instructions and GameDesc files use the
    /// canonical string form "(x,y,z)". Storing the array under a declared
    /// "String" type produced mixed pairs such as
    /// <code>Position: Type=String, Value=[276,155.25,0]</code>
    /// which later crashed instruction consumers that expected a scalar.
    ///
    /// Two entry points cover both directions of that round-trip:
    ///  - <see cref="Coerce"/> normalizes a value before it is stored.
    ///  - <see cref="ToInstructionValue"/> normalizes a stored value before it
    ///    is emitted as an instruction parameter.
    /// </summary>
    public static class GameDescPropertyValue
    {
        /// <summary>
        /// True when the declared type represents a scalar string-like value
        /// that can never legitimately hold a JSON container.
        /// </summary>
        public static bool IsStringType(string declaredType)
        {
            if (string.IsNullOrWhiteSpace(declaredType))
                return false;

            return string.Equals(declaredType, "String", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(declaredType, "ObjectRef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(declaredType, "SpaceRef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(declaredType, "AssetRef", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Coerces <paramref name="value"/> into the representation implied by
        /// <paramref name="declaredType"/>. Arrays are written as "(x,y,z)" and
        /// other containers as compact JSON when the declared type is
        /// string-like. Values that already match, and values whose declared
        /// type is not string-like, are returned unchanged.
        /// </summary>
        public static JToken Coerce(string declaredType, JToken value)
        {
            if (value == null)
                return null;

            if (value.Type == JTokenType.Null || value.Type == JTokenType.Undefined)
                return value;

            if (!IsStringType(declaredType) || value.Type == JTokenType.String)
                return value;

            return new JValue(ToDisplayString(value));
        }

        /// <summary>
        /// Produces the canonical instruction representation of a persisted
        /// value. Executors consume parameters either as strings or as Vector3
        /// tokens, so JSON containers are flattened here:
        /// numeric arrays become "(x,y,z)"; other arrays/objects become compact
        /// JSON strings. Scalars are returned unchanged.
        /// </summary>
        public static JToken ToInstructionValue(JToken value)
        {
            if (value == null)
                return null;

            switch (value.Type)
            {
                case JTokenType.Array:
                    if (TryFormatNumericArray((JArray)value, out string vector))
                        return new JValue(vector);

                    return new JValue(value.ToString(Formatting.None));

                case JTokenType.Object:
                    return new JValue(value.ToString(Formatting.None));

                default:
                    return value;
            }
        }

        /// <summary>
        /// Best-effort scalar text for a token: numeric arrays become
        /// "(x,y,z)", containers become compact JSON, everything else uses its
        /// invariant-culture text form.
        /// </summary>
        public static string ToDisplayString(JToken value)
        {
            if (value == null)
                return string.Empty;

            switch (value.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return string.Empty;

                case JTokenType.String:
                    return value.Value<string>() ?? string.Empty;

                case JTokenType.Array:
                    return TryFormatNumericArray((JArray)value, out string vector)
                        ? vector
                        : value.ToString(Formatting.None);

                case JTokenType.Object:
                    return value.ToString(Formatting.None);

                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return Convert.ToString(
                        ((JValue)value).Value,
                        CultureInfo.InvariantCulture) ?? string.Empty;

                default:
                    return value.ToString(Formatting.None);
            }
        }

        private static bool TryFormatNumericArray(JArray array, out string text)
        {
            text = null;

            if (array == null || array.Count == 0)
                return false;

            var parts = new string[array.Count];

            for (int i = 0; i < array.Count; i++)
            {
                JToken item = array[i];
                if (item == null ||
                    (item.Type != JTokenType.Integer && item.Type != JTokenType.Float))
                {
                    return false;
                }

                parts[i] = Convert.ToString(
                    ((JValue)item).Value,
                    CultureInfo.InvariantCulture);
            }

            text = "(" + string.Join(",", parts) + ")";
            return true;
        }
    }
}
