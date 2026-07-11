using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.create_from)]
    public class Executor_CreateFrom : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string gameDescPathFile = instruction.parameters.GetString("GameDesc");
            string spacesStr = instruction.parameters.GetString("Spaces");

            List<Instruction> instructions = new List<Instruction>();

            Instruction subInstruction = new Instruction()
            { 
                type = Instruction.k_TypeInstructionList,
                state = Instruction.eState.Resolved
            };

            var genInstructions = GameDescManager.Instance.GetInstructionsForSpaces(gameDescPathFile, spacesStr);

            if (genInstructions != null && genInstructions.Length > 0)
            {
                List<Instruction> batchInstructions = new List<Instruction>(genInstructions);

                var importAssetsInstruction = new Instruction()
                {
                    action = T2G.Actions.import_assets,
                    state = Instruction.eState.Resolved,
                    assets = new List<string>()
                };

                T2G.Utils.CollectAllAssets(genInstructions, ref importAssetsInstruction.assets);

                batchInstructions.Insert(0, importAssetsInstruction);

                batchInstructions.Add(new Instruction()
                {
                    action = T2G.Actions.save_space,
                    state = Instruction.eState.Resolved
                });

                instruction.instructions = batchInstructions.ToArray();
            }

            instruction.instructions = new Instruction[1] { subInstruction };

            return (true, $"{instruction.instructions.Length} instructions are queued to be executed:", null);
        }
    }
}