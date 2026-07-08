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

            var instruction = new Instruction()
            {
                type = Instruction.k_TypeInstructionList,
                action = GetActionName(),
            };

            var genInstructions = GameDescManager.Instance.GetInstructionsForSpaces(gameDescPathFile, spacesStr);

            List<Instruction> instructions = new List<Instruction>();
            
            if(genInstructions != null && genInstructions.Length > 0)
            {
                List<Instruction> batchInstructions = new List<Instruction>(genInstructions);

                var importAssetsInstruction = new Instruction()
                {
                    action = T2G.Actions.import_assets,
                    state = Instruction.eState.Resolved,
                    assets = new List<string>()
                };
                
                T2G.Utils.CollectAllAssets(genInstructions, ref importAssetsInstruction.assets);
                Debug.Log($"{importAssetsInstruction.assets.Count} assets need to be imported.");

                batchInstructions.Insert(0, importAssetsInstruction);

                batchInstructions.Add(new Instruction()
                {
                    action = T2G.Actions.save_space,
                    state = Instruction.eState.Resolved
                });

                instruction.instructions = batchInstructions.ToArray();
                

                instructions.Add(instruction);
                return (true, instructions);
            }

            return (false, null);
        }
    }
}
