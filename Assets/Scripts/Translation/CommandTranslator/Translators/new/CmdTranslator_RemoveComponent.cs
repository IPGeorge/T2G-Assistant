using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.remove_component)]
    public class CmdTranslator_RemoveComponent : CmdTranslatorBase
   {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            string componentType = Utils.GetParamFromArguments(args, "componentType");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            instruction.parameters = new List<ValuePair>();
            instruction.parameters.Add(new ValuePair("objName", objName));
            instruction.parameters.Add(new ValuePair("componentType", componentType));
            instructions.Add(instruction);

            return (true, instructions);
        }
    }
}
