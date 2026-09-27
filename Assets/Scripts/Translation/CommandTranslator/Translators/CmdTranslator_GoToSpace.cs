using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator("goto_space")]
    public class CmdTranslator_GoToSpace : CmdTranslatorBase
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