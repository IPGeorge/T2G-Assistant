using System;
using System.Collections.Generic;

namespace T2G.Assistant
{

    [CommandTranslator(T2G.Actions.create_object)]
    public class CmdTranslator_CreateObject : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Raw;
            instruction.parameters = new List<ValuePair>();

            string objectName = Utils.GetParamFromArguments(args, "name").Trim();
            if(string.IsNullOrEmpty(objectName))
            {
                objectName = "Obj_" + Guid.NewGuid().ToString("N");  //use 32 characters without hyphens format
            }
            instruction.desc = Utils.GetParamFromArguments(args, "desc").Trim();
            instruction.parameters.Add(new ValuePair("Name", objectName));

            // Handle optional position parameters
            string x = Utils.GetParamFromArguments(args, "x");
            string y = Utils.GetParamFromArguments(args, "y");
            string z = Utils.GetParamFromArguments(args, "z");
            if (!string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(y) && !string.IsNullOrEmpty(z))
            {
                instruction.parameters.Add(new ValuePair("position", $"({x}, {y}, {z})"));
            }

            instructions.Add(instruction);

            return (true, instructions);
        }
    }
}