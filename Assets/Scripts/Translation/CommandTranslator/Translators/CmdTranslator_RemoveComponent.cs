using System.Collections.Generic;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.remove_component)]
    public class CmdTranslator_RemoveComponent : CmdTranslatorBase
   {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            string componentType = Utils.GetParamFromArguments(args, "componentType");
            string objName = Utils.GetParamFromArguments(args, "objName");

            if (string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(objName))
            {
                return (false, null);
            }

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();

            instruction.parameters.Add(new Instruction.Parameter("ObjName", objName));
            instruction.parameters.Add(new Instruction.Parameter("ComponentType", componentType));

            return (true, instruction);
        }
    }
}
