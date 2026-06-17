using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.generate_from)]
    public class CmdTranslator_GenerateFrom : CmdTranslatorBase
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

            var instruction = new Instruction
            {
                action = GetActionName(),
                state = Instruction.eState.Batch
            };

            instruction.instructions = GameDescManager.Instance.GetInstructionsForSpaces(gameDescPathFile, spacesStr);

            var saveInst = new Instruction()
            {
                action = T2G.Actions.save_space,
                state = Instruction.eState.Resolved
            };
            
            List<Instruction> instructions = new List<Instruction>() { instruction, saveInst };
            return (true, instructions);
        }
    }
}
