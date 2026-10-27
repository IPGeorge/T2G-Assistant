using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using T2G;

namespace T2G.Assistant
{
    public static class TranslationEnvelopeParser
    {
        public static TranslationResponse Parse(string json)
        {
            var root = JObject.Parse(json);
            string kind = root.Value<string>("responseType")?.Trim().ToLowerInvariant();
            switch (kind)
            {
                case "instruction":
                    JToken token = root["instruction"];
                    if (token == null || token.Type != JTokenType.Object)
                        throw new JsonException("Instruction response requires an instruction object.");
                    InstructionBase instruction = InstructionJsonParser.Parse(token.ToString(Formatting.None));
                    var result = new TranslationResponse { Type = TranslationResponseType.Instruction };
                    result.Instructions.Add(instruction);
                    return result;
                case "clarification":
                case "conversation":
                    string message = root.Value<string>("message");
                    if (string.IsNullOrWhiteSpace(message))
                        throw new JsonException(kind + " response requires a message.");
                    return new TranslationResponse
                    {
                        Type = kind == "clarification" ? TranslationResponseType.Clarification : TranslationResponseType.Conversation,
                        Message = message
                    };
                default:
                    throw new JsonException("Unknown responseType: " + kind);
            }
        }
    }
}
