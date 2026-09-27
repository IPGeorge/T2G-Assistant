using System.Collections.Generic;
using System.IO;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.update_component)]
    public class CmdTranslator_UpdateComponent : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            string component = Utils.GetParamFromArguments(args, "component");
            string newComponent = Utils.GetParamFromArguments(args, "newComponent");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(component) || string.IsNullOrEmpty(newComponent) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            instructions[0] = new Instruction();
            instructions[0].action = T2G.Actions.remove_component;
            instructions[0].parameters.Add(new Instruction.Parameter("objName", objName));
            instructions[0].parameters.Add(new Instruction.Parameter("componentType", component));

            instructions[1] = new Instruction();
            instructions[1].action = T2G.Actions.add_component;
            if (PathValidator.IsValidFilePath(newComponent, true))
            {
                instructions[1].parameters.Add(new Instruction.Parameter("Type", "file"));
            }
            else
            {
                if (ComponentResolver.IsValidComponentName(newComponent))
                {
                    instructions[1].parameters.Add(new Instruction.Parameter("Type", "component"));
                }
                else
                {
                    instructions[1].parameters.Add(new Instruction.Parameter("Type", "asset"));
                }
            }
            instructions[1].parameters.Add(new Instruction.Parameter("objName", objName));
            instructions[1].parameters.Add(new Instruction.Parameter("component", newComponent));

            InstructionSequence instructionSequence = new InstructionSequence()
            {
                instructions = instructions
            };

            return (true, instructionSequence);
        }
    }
}

