using System.Collections.Generic;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.clear)]
    public class CmdTranslator_Clear : CmdTranslatorBase
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