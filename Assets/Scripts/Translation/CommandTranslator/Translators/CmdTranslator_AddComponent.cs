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
                instruction.state = Instruction.eState.Resolved;
                instruction.desc = "file";
            }
            else
            {
                instruction.parameters = new List<ValuePair>();
                instruction.parameters.Add(new ValuePair("objName", objName));
                instruction.parameters.Add(new ValuePair("component", component));

                // Only mark as resolved if it's a known standard Unity component
                // Skip using ComponentResolver due to false positive partial matching
                // Use specific checks instead
                string lower = component.ToLowerInvariant();
                if (IsKnownUnityComponent(lower))
                {
                    instruction.state = Instruction.eState.Resolved;
                    instruction.desc = "component";
                }
                else
                {
                    // Custom script - go to Raw for AssetSearchClient resolution
                    instruction.state = Instruction.eState.Raw;
                    instruction.desc = component;
                }
            }
            instructions.Add(instruction);

            return (true, instructions);
        }

        private bool IsKnownUnityComponent(string component)
        {
            // Known standard Unity components (be careful with partial names)
            string[] knownComponents = new[]
            {
                "rigidbody", "rigidbody2d",
                "boxcollider", "spherecollider", "capsulecollider", "meshcollider", "wheelcollider",
                "boxcollider2d", "spherecollider2d", "capsulecollider2d", "polygoncollider2d",
                "charactercontroller",
                "transform", "camera", "light", "meshfilter", "meshrenderer",
                "animator", "animation", "audiolistener", "audiosource",
                "canvas", "canvasgroup", "canvasrenderer",
                "button", "text", "image", "rawimage", "slider", "scrollbar", "toggle",
                "dropdown", "inputfield", "scrollrect", "recttransform",
                "particlesystem", "particlesystemrenderer",
                "trailrenderer", "linerenderer", "skinnedmeshrenderer",
                "terrain", "terraincollider",
                "navmeshagent", "navmeshobstacle",
                "constantforce", "joint", "hingejoint", "fixedjoint", "springjoint",
                "cloth", "windzone", "reflectionprobe", "lodgroup"
            };

            // Exact match only (avoid "spin" matching "spherecollider")
            foreach (var known in knownComponents)
            {
                if (component == known)
                    return true;
            }

            return false;
        }
    }
}
