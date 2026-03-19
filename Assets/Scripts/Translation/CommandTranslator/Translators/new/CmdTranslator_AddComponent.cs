using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.add_component)]
    public class CmdTranslator_AddComponent : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            string component = Utils.GetParamFromArguments(args, "component");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(component) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            List<Instruction> instructions = new List<Instruction>();
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            if (PathValidator.IsValidFilePath(component, true))
            {
                instruction.state =  Instruction.eState.Resolved;
                instruction.desc = "file";
            }
            else
            {
                if (ComponentResolver.IsValidComponentName(component))
                {
                    instruction.state = Instruction.eState.Resolved;
                    instruction.desc = "component";
                }
                else  //should be resolved for a script file from ContentLibrary
                {
                    instruction.state = Instruction.eState.Raw;
                    instruction.desc = component;
                }
            }
            instruction.parameters = new List<ValuePair>();
            instruction.parameters.Add(new ValuePair("objName", objName));
            instruction.parameters.Add(new ValuePair("component", component));
            instructions.Add(instruction);

            return (true, instructions);
        }
    }
}
