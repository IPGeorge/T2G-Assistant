using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace T2G.Assistant
{
    /// <summary>Parses one root Instruction or InstructionSequence without flattening it.</summary>
    public static class InstructionJsonParser
    {
        public static InstructionBase Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty instruction JSON.");
            var root = JToken.Parse(json);
            if (root.Type != JTokenType.Object) throw new JsonException("Expected a JSON object as the root.");
            return ParseObject((JObject)root);
        }

        private static InstructionBase ParseObject(JObject obj)
        {
            var typeToken = obj.GetValue("type", StringComparison.OrdinalIgnoreCase);
            if (typeToken == null) throw new JsonException("Missing instruction type.");
            InstructionType type;
            if (typeToken.Type == JTokenType.Integer)
            {
                int numericType = typeToken.Value<int>();
                if (!Enum.IsDefined(typeof(InstructionType), numericType))
                    throw new JsonException("Unknown instruction type: " + numericType);
                type = (InstructionType)numericType;
            }
            else if (!Enum.TryParse(typeToken.ToString(), true, out type) || !Enum.IsDefined(typeof(InstructionType), type))
                throw new JsonException("Unknown instruction type: " + typeToken);

            if (type == InstructionType.Sequence)
            {
                var children = obj.GetValue("instructions", StringComparison.OrdinalIgnoreCase) as JArray;
                if (children == null || children.Count == 0)
                    throw new JsonException("Sequence must contain a nonempty instructions array.");
                var seq = new InstructionSequence
                {
                    id = obj.GetValue("id", StringComparison.OrdinalIgnoreCase)?.ToString() ?? string.Empty,
                    desc = obj.GetValue("desc", StringComparison.OrdinalIgnoreCase)?.ToString() ?? string.Empty
                };
                foreach (var child in children)
                {
                    if (!(child is JObject childObj)) throw new JsonException("Sequence child must be an object.");
                    var parsed = ParseObject(childObj);
                    if (!(parsed is Instruction instruction))
                        throw new JsonException("Nested sequences are not supported by InstructionSequence.instructions.");
                    seq.instructions.Add(instruction);
                }
                return seq;
            }

            // Use the existing InstructionConverter to normalize legacy aliases.
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new InstructionConverter() },
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            var result = JsonConvert.DeserializeObject<Instruction>(obj.ToString(Formatting.None), settings);
            if (result == null || string.IsNullOrWhiteSpace(result.action))
                throw new JsonException("An individual instruction must have an action.");
            result.type = type;
            return result;
        }
    }
}
