using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.create_from)]
    public class CmdTranslator_CreateFrom : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            // 1. Parse parameters
            string gameDescPathFile = Utils.GetParamFromArguments(args, "gamedesc");
            string spacesStr = Utils.GetParamFromArguments(args, "spaces");

            if (string.IsNullOrEmpty(gameDescPathFile) || !File.Exists(gameDescPathFile))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            var instruction = new Instruction()
            {
                type = Instruction.k_TypeInstruction,
                action = GetActionName(),
                state = Instruction.eState.Local,
                parameters = new List<ValuePair>()
            };

            instruction.parameters.Add(new ValuePair("GameDesc", gameDescPathFile));
            instruction.parameters.Add(new ValuePair("Spaces", spacesStr));

            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}
