using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.attach_to)]
    public class CmdTranslator_Attach : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            string source = Utils.GetParamFromArguments(args, "source");
            string target = Utils.GetParamFromArguments(args, "target");
            string socket = Utils.GetParamFromArguments(args, "socket");
            
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                return (false, null);
            }
            
            instruction.parameters.Add(new Instruction.Parameter("Source", source));
            instruction.parameters.Add(new Instruction.Parameter("Target", target));
            
            if (!string.IsNullOrEmpty(socket))
            {
                instruction.parameters.Add(new Instruction.Parameter("Socket", socket));
            }
            
            return (true, instruction);
        }
    }
}