using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.attach_to)]
    public class CmdTranslator_Attach : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Resolved;
            string source = Utils.GetParamFromArguments(args, "source");
            string target = Utils.GetParamFromArguments(args, "target");
            string socket = Utils.GetParamFromArguments(args, "socket");
            
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                return (false, null);
            }
            
            instruction.parameters = new List<ValuePair>();
            instruction.parameters.Add(new ValuePair("Source", source));
            instruction.parameters.Add(new ValuePair("Target", target));
            
            if (!string.IsNullOrEmpty(socket))
            {
                instruction.parameters.Add(new ValuePair("Socket", socket));
            }
            
            instructions.Add(instruction);
            return (true, instructions);
        }
    }
}