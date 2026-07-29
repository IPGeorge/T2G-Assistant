using System;
using System.Collections.Generic;
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
                state = Instruction.eState.Resolved
            };
            spaceInstr.parameters = new List<ValuePair>
            {
                new ValuePair("SpaceName", space.Name)
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
                state = Instruction.eState.Resolved,
                desc = obj.Desc ?? string.Empty
            };
            createInstr.parameters = new List<ValuePair>
            {
                new ValuePair("Name", obj.Name)
            };

            // Tags
            if (obj.Tags != null && obj.Tags.Count > 0)
            {
                createInstr.parameters.Add(new ValuePair("Tags", string.Join(",", obj.Tags)));
            }

            // Roles
            if (obj.Roles != null && obj.Roles.Count > 0)
            {
                createInstr.parameters.Add(new ValuePair("Roles", string.Join(",", obj.Roles)));
            }

            // Assets — resolve keys through Space.Assets, emit [import, load] pairs
            if (obj.Assets != null && obj.Assets.Count > 0 && space?.Assets != null)
            {
                createInstr.assets = new List<string>(obj.Assets.Count * 2);
                foreach (var key in obj.Assets)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (space.Assets.TryGetValue(key, out var info))
                    {
                        createInstr.assets.Add(info.ImportPath ?? key);
                        createInstr.assets.Add(info.LoadPath ?? key);
                    }
                    else
                    {
                        createInstr.assets.Add(key);
                    }
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
                            state = Instruction.eState.Resolved
                        };
                        attachInstr.parameters = new List<ValuePair>
                        {
                            new ValuePair("source", obj.Name),
                            new ValuePair("target", targetName)
                        };
                        if (!string.IsNullOrWhiteSpace(rel.Slot))
                        {
                            attachInstr.parameters.Add(new ValuePair("socket", rel.Slot));
                        }
                        result.Add(attachInstr);
                    }
                    else
                    {
                        // Other relationship types → set_relationship instruction
                        var relInstr = new Instruction
                        {
                            action = T2G.Actions.set_relationship,
                            state = Instruction.eState.Resolved
                        };
                        relInstr.parameters = new List<ValuePair>
                        {
                            new ValuePair("source", obj.Name),
                            new ValuePair("type", rel.Type),
                            new ValuePair("target", targetName)
                        };
                        if (!string.IsNullOrWhiteSpace(rel.Slot))
                        {
                            relInstr.parameters.Add(new ValuePair("slot", rel.Slot));
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
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("objName", obj.Name),
                        new ValuePair("Property", prop.name),
                        new ValuePair("Value", prop.value)
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
                action = T2G.Actions.add_script,
                state = Instruction.eState.Resolved,
                assets = component.Assets
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

            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", objectName),
                new ValuePair("type", mechanism)
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
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("Name", objectName),
                        new ValuePair("Property", $"{component.Type}.{prop.Name}"),
                        new ValuePair("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            if (subInstructions.Count > 0)
                instr.instructions = subInstructions.ToArray();

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
                action = T2G.Actions.add_script,
                state = Instruction.eState.Resolved
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

            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", spaceName),
                new ValuePair("type", mechanism)
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
                        state = Instruction.eState.Resolved
                    };
                    setProp.parameters = new List<ValuePair>
                    {
                        new ValuePair("Name", spaceName),
                        new ValuePair("Property", $"{component.Type}.{prop.Name}"),
                        new ValuePair("Value", prop.Value)
                    };
                    subInstructions.Add(setProp);
                }
            }

            if (subInstructions.Count > 0)
                instr.instructions = subInstructions.ToArray();

            return instr;
        }
    }
}
