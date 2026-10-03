using System.Collections.Generic;
using System.IO;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.place_on)]
    public class CmdTranslator_PlaceOn : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.type = InstructionType.Composite;
            string objectName = Utils.GetParamFromArguments(args, "name");
            instruction.parameters.Add(new Instruction.Parameter("Name", objectName));
            return (true, instruction);
        }
    }
}