using System.Collections.Generic;
using System.IO;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.create_from)]
    public class CmdTranslator_CreateFrom : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Batch;
            instruction.parameters = new List<ValuePair>();
            string gameDescFile = Utils.GetParamFromArguments(args, "gameDesc");
            var gameDescManager = Assistant.Instance.GameDescManager;
            if (gameDescManager != null && !string.IsNullOrWhiteSpace(gameDescFile))
            {
                var batchInstructions = gameDescManager.GetInstructionsFromGameDesc(gameDescFile);
                if (batchInstructions != null && batchInstructions.Length > 0)
                {
                    instruction.instructions = batchInstructions;
                    instructions.Add(instruction);
                    return (true, instructions);
                }
            }
            return (false, null);
        }
    }
}