using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{
    [CommandTranslator("create_space")]
    public class CmdTranslator_CreateSpace : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();

            string spaceName = Utils.GetParamFromArguments(args, "name");
            instruction.parameters.Add(new Instruction.Parameter("SpaceName", spaceName));

            return (true, instruction);
        }
    }
}