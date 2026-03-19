using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.update_component)]
    public class CmdTranslator_UpdateComponent : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            string component = Utils.GetParamFromArguments(args, "component");
            string newComponent = Utils.GetParamFromArguments(args, "newComponent");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(component) || string.IsNullOrEmpty(newComponent) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.state = Instruction.eState.Batch;

            instruction.instructions = new Instruction[2];
            instruction.instructions[0] = new Instruction();
            instruction.instructions[0].action = T2G.Actions.remove_component;
            instruction.instructions[0].state = Instruction.eState.Resolved;
            instruction.instructions[0].parameters = new List<ValuePair>();
            instruction.parameters.Add(new ValuePair("objName", objName));
            instruction.parameters.Add(new ValuePair("componentType", component));

            instruction.instructions[1] = new Instruction();
            instruction.instructions[1].action = T2G.Actions.add_component;
            if (PathValidator.IsValidFilePath(newComponent, true))
            {
                instruction.instructions[1].state = Instruction.eState.Resolved;
                instruction.instructions[1].desc = "file";
            }
            else
            {
                if (ComponentResolver.IsValidComponentName(component))
                {
                    instruction.instructions[1].state = Instruction.eState.Resolved;
                    instruction.instructions[1].desc = "component";
                }
                else
                {
                    instruction.instructions[1].state = Instruction.eState.Raw;
                    instruction.instructions[1].desc = component;
                }
            }
            instruction.instructions[1].parameters = new List<ValuePair>();
            instruction.instructions[1].parameters.Add(new ValuePair("objName", objName));
            instruction.instructions[1].parameters.Add(new ValuePair("component", newComponent));

            instructions.Add(instruction);
          
            return (true, instructions);
        }
    }
}

