using System.Collections.Generic;
using System.IO;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.set_property)]
    public class CmdTranslator_Set : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            instruction.parameters = new List<ValuePair>();

            string objName = Utils.GetParamFromArguments(args, "objName").Trim();
            string propertyName = Utils.GetParamFromArguments(args, "property").Trim();
            string valueString = Utils.GetParamFromArguments(args, "value").Trim();

            if(string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(propertyName))
            {
                return (false, null);
            }

            instruction.parameters.Add(new ValuePair("objName", objName));
            instruction.parameters.Add(new ValuePair("property", propertyName));
            instruction.parameters.Add(new ValuePair("value", valueString));
            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}
