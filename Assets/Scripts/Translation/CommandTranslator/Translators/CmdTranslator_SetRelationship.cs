using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.set_relationship)]
    public class CmdTranslator_SetRelationship : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            string source = Utils.GetParamFromArguments(args, "source");
            string target = Utils.GetParamFromArguments(args, "target");
            string type = Utils.GetParamFromArguments(args, "type");
            string slot = Utils.GetParamFromArguments(args, "slot");

            if (string.IsNullOrEmpty(source))
                return (false, null);

            var instruction = new Instruction
            {
                action = GetActionName(),
                type = InstructionType.Composite,
                parameters = new List<Instruction.Parameter>
                {
                    new Instruction.Parameter("source", source),
                    new Instruction.Parameter("target", target ?? string.Empty),
                    new Instruction.Parameter("type", type ?? string.Empty),
                    new Instruction.Parameter("slot", slot ?? string.Empty)
                }
            };

            return (true, instruction);
        }
    }
}
