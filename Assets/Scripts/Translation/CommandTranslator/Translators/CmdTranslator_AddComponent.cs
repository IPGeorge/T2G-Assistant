using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.add_component)]
    public class CmdTranslator_AddComponent : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            string component = Utils.GetParamFromArguments(args, "component");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(component) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.parameters.Add(new Instruction.Parameter("ObjName", objName));
            instruction.desc = component;

            if (PathValidator.IsValidFilePath(component, true))
            {
                instruction.parameters.Add(new Instruction.Parameter("Type", "file"));           
            }
            else
            {
                string newComponent = component.ToLowerInvariant();
                if (ComponentResolver.IsValidComponentName(newComponent))
                {
                    instruction.parameters.Add(new Instruction.Parameter("Type", "component"));  
                }
                else
                {
                    instruction.parameters.Add(new Instruction.Parameter("Type", "asset"));      //Will need semantic resolution
                }
            }

            return (true, instruction);
        }
    }
}
