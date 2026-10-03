using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.select_object)]
    public class CmdTranslator_SelectObject : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            string objectName = Utils.GetParamFromArguments(args, "name");
            instruction.parameters.Add(new Instruction.Parameter("Name", objectName));
            return (true, instruction);
        }
    }
}