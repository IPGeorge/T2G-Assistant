using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace T2G
{

    #region Instruction models

    [Serializable]
    public class Instruction
    {
        public enum eState
        {
            Invalid = -1,
            Init = 0,
            Local,
            Raw,
            Resolved,
            Batch
        }

        public string type = "Instruction";
        public string action;
        public eState state = eState.Init;
        public string desc;
        
        [JsonProperty("parameters")] // Model may output "params" or "parameters" → accept both.
        public List<ValuePair> parameters;

        
        [JsonProperty("assets")] // Model may output "assets", "Assets" → accept both (case-insensitive is the default).
        public List<string> assets;

        public Instruction[] instructions;
    }

    [Serializable]
    public class ValuePair
    {
        public string name;
        public JToken value;
        public ValuePair(string keyName, JToken keyValue)
        {
            name = keyName;
            value = keyValue;
        }
    }

    public class InstructionList
    {
        public string type = "InstructionList";
        public List<Instruction> instructions;
    }

    #endregion Instruction models

    public class InstructionConverter : JsonConverter<Instruction>
    {
        public override Instruction ReadJson(JsonReader reader, Type objectType, Instruction existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);

            // Alias: "params" -> "parameters"
            if (obj["parameters"] == null && obj["params"] != null)
                obj["parameters"] = obj["params"];

            // Alias: "Assets" -> "assets"
            if (obj["assets"] == null && obj["Assets"] != null)
                obj["assets"] = obj["Assets"];

            // Alias: some models emit "Params" etc.; add more if needed.

            var inst = new Instruction();
            serializer.Populate(obj.CreateReader(), inst);
            return inst;
        }

        public override void WriteJson(JsonWriter writer, Instruction value, JsonSerializer serializer)
        {
            var obj = JObject.FromObject(value, serializer);
            obj.WriteTo(writer);
        }
    }


}
