using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.detach_from)]
    public class CmdTranslator_Detach : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            string objName = Utils.GetParamFromArguments(args, "name");
            if (string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }
            instruction.parameters.Add(new Instruction.Parameter("Name", objName));
            return (true, instruction);
        }
    }
}
