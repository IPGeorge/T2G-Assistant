using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{

    [CommandTranslator(T2G.Actions.create_object)]
    public class CmdTranslator_CreateObject : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Raw;
            instruction.parameters = new List<ValuePair>();
            string objectName = Utils.GetParamFromArguments(args, "name");
            string objectDesc = Utils.GetParamFromArguments(args, "desc");
            instruction.parameters.Add(new ValuePair("objectName", objectName));
            instruction.parameters.Add(new ValuePair("objectDesc", objectDesc));
            instructions.Add(instruction);

            return (true, instructions);
        }
    }
}