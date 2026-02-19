using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator("goto_space")]
    public class CmdTranslator_GoToSpace : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetType().GetCustomAttribute<CommandTranslatorAttribute>()?.Action;
            instruction.state = Instruction.eState.Resolved;
            instruction.parameters = new List<ValuePair>();

            string spaceName = Utils.GetParamFromArguments(args, "name");
            instruction.parameters.Add(new ValuePair("spaceName", spaceName));

            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}