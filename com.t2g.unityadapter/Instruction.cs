using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

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
            Local,          //Is a local executed instruction
            Raw,            //An instruction without assets information
            Resolved        //A resolved instruction with assets inforamtion
        }

        public const int k_TypeInstruction = 0;
        public const int k_TypeInstructionList = 1;  //No action instruction only contains sub-instructions

        public int type = k_TypeInstruction;
        public string action;
        public eState state = eState.Init;
        public string desc;          //Description. (e.g., a swat soldier character)
        
        [JsonProperty("parameters")] // Model may output "params" or "parameters" → accept both.
        public List<ValuePair> parameters;
       
        [JsonProperty("assets")]     //Model may output "assets", "Assets" → accept both (case-insensitive is the default).
        public List<string> assets;  //resolved assets needs for this instruction (e.g., swat.unitypackage, SwatController.cs)   

        [SerializeReference]
        public Instruction[] instructions;  //sub instructions

        public Instruction CloneWithoutSubInstructions()
        {
            Instruction inst = new Instruction();
            inst.action = this.action;
            inst.state = this.state;
            inst.desc = this.desc;
            if (this.parameters != null)
            {
                inst.parameters = new List<ValuePair>(this.parameters);
            }
            if (this.assets != null)
            {
                inst.assets = new List<string>(this.assets);
            }
            return inst;
        }
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
