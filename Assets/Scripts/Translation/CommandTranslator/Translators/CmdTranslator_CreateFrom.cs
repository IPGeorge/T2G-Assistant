using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.create_from)]
    public class CmdTranslator_CreateFrom : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            // 1. Parse parameters
            string gameDescPathFile = Utils.GetParamFromArguments(args, "gamedesc");
            string spacesStr = Utils.GetParamFromArguments(args, "spaces");

            if (string.IsNullOrEmpty(gameDescPathFile) || !File.Exists(gameDescPathFile))
            {
                return (false, null);
            }

            var instruction = new Instruction()
            {
                type = InstructionType.Local,
                action = GetActionName(),
            };

            instruction.parameters.Add(new Instruction.Parameter("GameDesc", gameDescPathFile));
            instruction.parameters.Add(new Instruction.Parameter("Spaces", spacesStr));

            return (true, instruction);
        }
    }
}
