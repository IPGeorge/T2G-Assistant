using System.Collections.Generic;
using System.Linq;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.call_method)]
    public class CmdTranslator_CallMethod : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            string objName = Utils.GetParamFromArguments(args, "name");
            string method = Utils.GetParamFromArguments(args, "method");
            string parameters = Utils.GetParamFromArguments(args, "parameters");

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(method))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            instruction.parameters = new List<ValuePair>();

            instruction.parameters.Add(new ValuePair("objName", objName));
            instruction.parameters.Add(new ValuePair("method", method));

            if (!string.IsNullOrEmpty(parameters))
            {
                var paramPairs = parameters.Split(',');
                foreach (var pair in paramPairs)
                {
                    var trimmed = pair.Trim();
                    var keyValue = trimmed.Split('=');
                    if (keyValue.Length == 2)
                    {
                        instruction.parameters.Add(new ValuePair(keyValue[0].Trim(), keyValue[1].Trim()));
                    }
                }
            }

            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}