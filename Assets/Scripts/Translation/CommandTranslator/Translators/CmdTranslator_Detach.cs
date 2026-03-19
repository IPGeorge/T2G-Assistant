using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.detach)]
    public class CmdTranslator_Dettach : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            string objName = Utils.GetParamFromArguments(args, "name");
            if (string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }
            instruction.parameters = new List<ValuePair>();
            instruction.parameters.Add(new ValuePair("name", objName));
            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}
