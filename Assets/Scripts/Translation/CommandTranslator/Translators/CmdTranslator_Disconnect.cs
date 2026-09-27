using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator("disconnect")]
    public class CmdTranslator_Disconnect : CmdTranslatorBase
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