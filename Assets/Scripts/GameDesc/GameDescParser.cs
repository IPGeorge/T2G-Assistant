using System;
using System.Collections.Generic;
using System.Linq;
using T2G;

namespace T2G.Assistant
{
    /// <summary>
    /// Parses a flat (machine-oriented) GameDesc into instruction lists
    /// for batch execution.
    /// </summary>
    public class GameDescParser
    {
        // ============================================================
        // Entry points
        // ============================================================

        public static bool ParseForInstructions(GameDesc gameDesc, out Instruction[] instructions)
        {
            instructions = ParseForInstructions(gameDesc);
            return instructions != null;
        }

        public static Instruction[] ParseForInstructions(GameDesc gameDesc)
        {
            if (gameDesc == null)
                return null;

            var result = new List<Instruction>();
            foreach (var space in gameDesc.Spaces)
            {
                if (space == null) continue;

                var spaceInstructions = ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                {
                    result.AddRange(spaceInstructions);
                }
            }
            return result.ToArray();
        }

        // ============================================================
        // Space → flat Instruction[]
        // ============================================================

        public static Instruction[] ParseSpaceForInstructions(T2G.Assistant.Space space)
        {
            if (space == null) return null;

            var result = new List<Instruction>();

            // 1. create_space
            var spaceInstr = new Instruction
            {
                action = T2G.Actions.create_space,
                state = InstructionState.Resolved
            };
            spaceInstr.parameters = new List<Instruction.Parameter>()
            {
                new Instruction.Parameter("SpaceName", space.Name)
            };
            result.Add(spaceInstr);

            // Build GUID → Name map for resolving relationship targets in instructions
            var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in space.Objects.Values)
            {
                if (obj != null && !string.IsNullOrWhiteSpace(obj.Name))
                    guidToName[obj.Id] = obj.Name;
            }

            // 2. Each object in the flat list (no hierarchy traversal needed)
            foreach (var obj in space.Objects.Values)
            {
                if (obj == null) continue;
                result.AddRange(ParseObjectForInstructions(obj, space, guidToName));
            }

            // 3. Space-level components
            foreach (var comp in space.Components)
            {
                if (comp == null) continue;
                result.Add(ParseSpaceComponentForInstructions(comp, space.Name));
            }

            return result.ToArray();
        }

        // ============================================================
        // Object → flat Instruction[]
        // ============================================================

        public static Instruction[] ParseObjectForInstructions(T2G.Assistant.Object obj, T2G.Assistant.Space space, Dictionary<string, string> guidToName = null)
        {
            if (obj == null) return null;

            var result = new List<Instruction>();

            // 1. create_object
            var createInstr = new Instruction
            {
                action = T2G.Actions.create_object,
                state = InstructionState.Resolved,
                desc = obj.Desc ?? string.Empty
            };
            createInstr.parameters = new List<Instruction.Parameter>
            {
                new Instruction.Parameter("Name", obj.Name)
            };

            // Tags
            if (obj.Tags != null && obj.Tags.Count > 0)
            {
                createInstr.parameters.Add(new Instruction.Parameter("Tags", string.Join(",", obj.Tags)));
            }

            // Roles
            if (obj.Roles != null && obj.Roles.Count > 0)
            {
                createInstr.parameters.Add(new Instruction.Parameter("Roles", string.Join(",", obj.Roles)));
            }

            // Assets — emit [import, load] pairs from ObjectAssetRef
            if (obj.Assets != null && obj.Assets.Count > 0)
            {
                createInstr.assets = new List<Instruction.Asset>(obj.Assets.Count * 2);
                foreach (var assetRef in obj.Assets)
                {
                    if (assetRef == null || string.IsNullOrWhiteSpace(assetRef.Key)) 
                        continue;

                    createInstr.assets.Add(new Instruction.Asset 
                    { 
                        desc = string.Empty,        //TODO:
                        type = AssetType.Unknown,   //TODO:
                        source = assetRef.LoadPath ?? assetRef.Key 
                    });
                }
            }
            result.Add(createInstr);

            // 2. Relationships → attach_to / set_relationship instructions
            //    Target GUIDs are resolved to Names for the executor's scene lookup
            if (obj.Relationships != null)
            {
                foreach (var rel in obj.Relationships)
                {
                    if (rel == null || string.IsNullOrWhiteSpace(rel.Type) || string.IsNullOrWhiteSpace(rel.Target))
                        continue;

                    // Resolve target GUID → Name for instructions
                    string targetName = guidToName != null && guidToName.TryGetValue(rel.Target, out var tn)
                        ? tn : rel.Target;

                    if (string.Equals(rel.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rel.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase))
                    {
                        var attachInstr = new Instruction
                        {
                            action = T2G.Actions.attach_to,
                            state = InstructionState.Resolved
                        };
                        attachInstr.parameters = new List<Instruction.Parameter>
                        {
                            new Instruction.Parameter("source", obj.Name),
                            new Instruction.Parameter("target", targetName)
                        };
                        if (!string.IsNullOrWhiteSpace(rel.Slot))
                        {
                            attachInstr.parameters.Add(new Instruction.Parameter("socket", rel.Slot));
                        }
                        result.Add(attachInstr);
                    }
                    else
                    {
                        // Other relationship types → set_relationship instruction
                        var relInstr = new Instruction
                        {
                            action = T2G.Actions.set_relationship,
                            state = InstructionState.Resolved
                        };
                        relInstr.parameters = new List<Instruction.Parameter>
                        {
                            new Instruction.Parameter("source", obj.Name),
                            new Instruction.Parameter("type", rel.Type),
                            new Instruction.Parameter("target", targetName)
                        };
                        if (!string.IsNullOrWhiteSpace(rel.Slot))
                        {
                            relInstr.parameters.Add(new Instruction.Parameter("slot", rel.Slot));
                        }
                        result.Add(relInstr);
                    }
                }
            }

            // 3. Object-level components
            if (obj.Components != null)
            {
                foreach (var comp in obj.Components)
                {
                    if (comp == null) continue;
                    result.Add(ParseComponentForInstructions(comp, obj.Name));
                }
            }

            // 4. Object-level properties → set_property
            if (obj.Properties != null)
            {
                foreach (var prop in obj.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = InstructionState.Resolved
                    };
                    setProp.parameters = new List<Instruction.Parameter>
                    {
                        new Instruction.Parameter("objName", obj.Name),
                        new Instruction.Parameter("Property", prop.name),
                        new Instruction.Parameter("Value", prop.value)
                    };
                    result.Add(setProp);
                }
            }

            return result.ToArray();
        }

        // ============================================================
        // Component → Instruction (add_component with nested set_property)
        // ============================================================

        public static Instruction ParseComponentForInstructions(T2G.Assistant.Component component, string objectName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = InstructionState.Resolved,
                // assets = component.Assets?.Select(a => a.Key).ToList()    //TODO:
            };
            // Read SourceType with heuristic fallback for legacy data
            string mechanism = component.SourceType;
            if (string.IsNullOrWhiteSpace(mechanism))
            {
                if (component.Assets != null && component.Assets.Count > 0)
                    mechanism = "asset";
                else if (!string.IsNullOrWhiteSpace(component.Description) &&
                         component.Description.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    mechanism = "file";
                else
                    mechanism = "component";
            }

            instr.parameters = new List<Instruction.Parameter>
            {
                new Instruction.Parameter("objName", objectName),
                new Instruction.Parameter("type", mechanism)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            var subInstructions = new List<Instruction>();
            if (component.Properties != null)
            {
                foreach (var prop in component.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.Name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = InstructionState.Resolved
                    };
                    setProp.parameters = new List<Instruction.Parameter>
                    {
                        new Instruction.Parameter("Name", objectName),
                        new Instruction.Parameter("Property", $"{component.Type}.{prop.Name}"),
                        new Instruction.Parameter("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            return instr;
        }

        // ============================================================
        // Space-level component → Instruction
        // ============================================================

        public static Instruction ParseSpaceComponentForInstructions(T2G.Assistant.Component component, string spaceName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = InstructionState.Resolved
            };
            // Read SourceType with heuristic fallback for legacy data
            string mechanism = component.SourceType;
            if (string.IsNullOrWhiteSpace(mechanism))
            {
                if (component.Assets != null && component.Assets.Count > 0)
                    mechanism = "asset";
                else if (!string.IsNullOrWhiteSpace(component.Description) &&
                         component.Description.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    mechanism = "file";
                else
                    mechanism = "component";
            }

            instr.parameters = new List<Instruction.Parameter>
            {
                new Instruction.Parameter("objName", spaceName),
                new Instruction.Parameter("type", mechanism)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            var subInstructions = new List<Instruction>();
            if (component.Properties != null)
            {
                foreach (var prop in component.Properties)
                {
                    if (prop == null || string.IsNullOrWhiteSpace(prop.Name))
                        continue;

                    var setProp = new Instruction
                    {
                        action = T2G.Actions.set_property,
                        state = InstructionState.Resolved
                    };
                    setProp.parameters = new List<Instruction.Parameter>
                    {
                        new Instruction.Parameter("Name", spaceName),
                        new Instruction.Parameter("Property", $"{component.Type}.{prop.Name}"),
                        new Instruction.Parameter("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            return instr;
        }
    }
}
