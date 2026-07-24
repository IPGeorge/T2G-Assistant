using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.set_relationship)]
    public class CmdTranslator_SetRelationship : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
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
                state = Instruction.eState.Resolved,
                parameters = new List<ValuePair>
                {
                    new ValuePair("source", source),
                    new ValuePair("target", target ?? string.Empty),
                    new ValuePair("type", type ?? string.Empty),
                    new ValuePair("slot", slot ?? string.Empty)
                }
            };

            return (true, new List<Instruction> { instruction });
        }
    }
}
