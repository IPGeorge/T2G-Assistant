using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{
    [CommandTranslator("connect")]
    public class CmdTranslator_Connect : CmdTranslatorBase
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