using System.Collections.Generic;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.clear)]
    public class CmdTranslator_Clear : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Local;
            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}