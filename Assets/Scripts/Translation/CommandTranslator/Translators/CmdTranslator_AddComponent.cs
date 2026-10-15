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
            instruction.parameters.Add(new Instruction.Parameter("Name", objName));
            instruction.desc = component;

            var asset = new T2G.Instruction.Asset(instruction.desc);
            instruction.assets.Add(asset);

            if (PathValidator.IsValidFilePath(component, true))
            {
                asset.type = AssetType.ScriptFile;
            }
            else
            {
                string newComponent = component.ToLowerInvariant();
                if (ComponentResolver.IsValidComponentName(newComponent))
                {
                    asset.type = AssetType.Identifier;
                }
                else
                {
                    asset.type = AssetType.SourceAsset;      //Will need semantic resolution
                }
            }

            return (true, instruction);
        }
    }
}
