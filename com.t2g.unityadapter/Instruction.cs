using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace T2G
{
    public enum InstructionType
    {
        Elemental = 0,  // A directly executable elemental T2G operation.
        Composite = 1,  // A higher-level operation implemented by a corresponding composite executor/capability.
        Local = 2,      // An operation executed locally by T2G. It is not sent to the game engine.
        Sequence = 3    // A container holding multiple instructions generated from one user intent.
    }

    public enum InstructionState
    {
        Init = 0,   // Instruction has been created but has not yet completed interpretation.
        Raw = 1,    // The canonical instruction. Resolution has not yet been completed.
        Resolved = 2    // Resolution has completed and the instruction is ready for execution.
    }

    public enum AssetType
    {
        Unknown = 0,
        Identifier = 1,     //System component, such as Unity rigidbody, camera, etc.
        ScriptFile = 2,     //A local script file
        SourceAsset = 3     //A resolved asset that can be imported from AssetService
    }

    [Serializable]
    public abstract class InstructionBase
    {
        public string id;                       // Unique instruction identifier.
        public InstructionType type = InstructionType.Elemental;
        public string desc; // Original user prompt that produced this sequence.
    }

    [Serializable]
    public class Instruction : InstructionBase
    {
        [Serializable]
        public class Parameter
        {
            public string name;     // Parameter name defined by the action schema.
            public string type;     // Semantic value type. Examples: String, Int, Float, Bool, Vector3, ObjectRef, SpaceRef.
            public JToken value;    // Parameter value.

            public Parameter(string keyName, string type, JToken keyValue)
            {
                name = keyName;
                this.type = type;
                value = keyValue;
            }

            public Parameter(string keyName, JToken keyValue)
            {
                name = keyName;
                value = keyValue;
                type = keyValue != null ? keyValue.Type.ToString() : "Null";
            }
        }

        [Serializable]
        public class Asset
        {
            public string desc = string.Empty;      // Semantic description used for asset resolution.
            public AssetType type = AssetType.Unknown;    
            public string source = string.Empty;    // resolved asset identifier, path, or URL.
        }

        public InstructionState state = InstructionState.Init;
        public string action;                   // Canonical action name.

        [JsonProperty("parameters")]            // Model may output "params" or "parameters" → accept both.
        public List<Parameter> parameters = new List<Parameter>();   
                                                // Parameters required by the action.

        [JsonProperty("assets")]                // Model may output "assets", "Assets" → accept both.
        public List<Asset> assets = new List<Asset>();      
                                                // Asset requirements associated with the action.

        public List<string> dependsOn = new List<string>();  
                                                // IDs of instructions that must successfully complete.

        public bool HasAssetSource(Instruction.Asset asset)
        {
            foreach(var registeredAsset in assets)
            {
                if(string.Compare(registeredAsset.source, asset.source, true) == 0)
                {
                    return true;
                }
            }
            return false;
        }


        public Instruction Clone()
        {
            Instruction instruction = new Instruction();
            instruction.id = this.id;
            instruction.type = this.type;
            instruction.state = this.state;
            instruction.action = this.action;
            instruction.desc = this.desc;

            if (this.parameters != null)
            {
                instruction.parameters = new List<Parameter>(this.parameters);
            }

            if (this.assets != null)
            {
                instruction.assets = new List<Asset>(this.assets);
            }

            if(dependsOn != null)
            {
                instruction.dependsOn = new List<string>(this.dependsOn);
            }

            return instruction;
        }
    }

    [Serializable]
    public class InstructionSequence : InstructionBase
    {
        public List<Instruction> instructions = new();  // Ordered instructions required to satisfy the user intent.

        public InstructionSequence()
        {
            type = InstructionType.Sequence; // Always Sequence.
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

            if(obj["dependsOn"] == null && obj["DependsOn"] != null)
            {
                obj["dependsOn"] = obj["DependsOn"];
            }

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


    //=============================================================================================
    /*
            public enum eState
            {
                Invalid = -1,
                Init = 0,
                Local,          //Is a local executed instruction
                Raw,            //An instruction without assets information
                Resolved        //A resolved instruction with assets inforamtion
            }

            public const int k_TypeInstruction = 0;      //A normal instruction that may contain sub-instructions
            public const int k_TypeInstructionList = 1;  //No action instruction only contains sub-instructions
            public const int k_TypeQuestion = 2;         //Ask for critical information
            public const int k_TypeFailed = -1;          //Failed to generate the instruction

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
*/

}
