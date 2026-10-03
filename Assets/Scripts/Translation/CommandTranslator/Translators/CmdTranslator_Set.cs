using System.Collections.Generic;
using System.IO;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.set_property)]
    public class CmdTranslator_Set : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();

            string objName = Utils.GetParamFromArguments(args, "objName").Trim();
            string propertyName = Utils.GetParamFromArguments(args, "property").Trim();
            string valueString = Utils.GetParamFromArguments(args, "value").Trim();

            if(string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(propertyName))
            {
                return (false, null);
            }

            instruction.parameters.Add(new Instruction.Parameter("Name", objName));
            instruction.parameters.Add(new Instruction.Parameter("Property", propertyName));
            instruction.parameters.Add(new Instruction.Parameter("Value", valueString));
            
            return (true, instruction);
        }
    }
}
