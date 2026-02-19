using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator("disconnect")]
    public class CmdTranslator_Disconnect : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetType().GetCustomAttribute<CommandTranslatorAttribute>()?.Action;
            instruction.state = Instruction.eState.Local;
            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}