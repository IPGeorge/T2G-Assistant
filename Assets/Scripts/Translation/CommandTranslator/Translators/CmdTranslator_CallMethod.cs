using System.Collections.Generic;
using System.Linq;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.call_method)]
    public class CmdTranslator_CallMethod : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            string objName = Utils.GetParamFromArguments(args, "name");
            string method = Utils.GetParamFromArguments(args, "method");
            string parameters = Utils.GetParamFromArguments(args, "parameters");

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(method))
            {
                return (false, null);
            }

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.parameters.Add(new Instruction.Parameter("ObjName", objName));
            instruction.parameters.Add(new Instruction.Parameter("Method", method));

            if (!string.IsNullOrEmpty(parameters))
            {
                var paramPairs = parameters.Split(',');
                foreach (var pair in paramPairs)
                {
                    var trimmed = pair.Trim();
                    var keyValue = trimmed.Split('=');
                    if (keyValue.Length == 2)
                    {
                        instruction.parameters.Add(new Instruction.Parameter(keyValue[0].Trim(), keyValue[1].Trim()));
                    }
                }
            }

            return (true, instruction);
        }
    }
}