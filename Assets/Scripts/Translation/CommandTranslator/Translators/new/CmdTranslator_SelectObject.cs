using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.select_object)]
    public class CmdTranslator_SelectObject : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            instruction.parameters = new List<ValuePair>();
            string objectName = Utils.GetParamFromArguments(args, "name");
            instruction.parameters.Add(new ValuePair("objectName", objectName));
            instructions.Add(instruction);

            return (true, instructions);
        }
    }
}