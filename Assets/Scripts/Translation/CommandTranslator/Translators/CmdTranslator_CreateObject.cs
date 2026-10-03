using System;
using System.Collections.Generic;

namespace T2G.Assistant
{

    [CommandTranslator(T2G.Actions.create_object)]
    public class CmdTranslator_CreateObject : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();

            string objectName = Utils.GetParamFromArguments(args, "name").Trim();
            instruction.desc = Utils.GetParamFromArguments(args, "desc").Trim();

            if (string.IsNullOrEmpty(objectName))
            {
                if (instruction.desc.Length <= 10)
                {
                    objectName = instruction.desc;
                }
                else
                {
                    objectName = "Obj_" + Guid.NewGuid().ToString("N");  //use 32 characters without hyphens format
                }
            }

            // Handle optional position parameters
            string x = Utils.GetParamFromArguments(args, "x");
            string y = Utils.GetParamFromArguments(args, "y");
            string z = Utils.GetParamFromArguments(args, "z");
            if (!string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(y) && !string.IsNullOrEmpty(z))
            {
                instruction.parameters.Add(new Instruction.Parameter("Position", $"({x}, {y}, {z})"));
            }
            else
            {
                instruction.parameters.Add(new Instruction.Parameter("Position", $"(0, 0, 0)"));
            }

            instruction.parameters.Add(new Instruction.Parameter("Name", objectName));

            return (true, instruction);
        }
    }
}