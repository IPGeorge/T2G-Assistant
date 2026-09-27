using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{
    [CommandTranslator("connect")]
    public class CmdTranslator_Connect : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.type = InstructionType.Local;
            return (true, instruction);
        }
    }
}