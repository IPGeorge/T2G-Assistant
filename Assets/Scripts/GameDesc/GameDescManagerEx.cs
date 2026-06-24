using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using T2G;

namespace T2G.Assistant
{
    public sealed partial class GameDescManager
    {
        // ============================================================
        // From file (local buffer — never touches Snapshot)
        // ============================================================

        /// <summary>
        /// Loads a GameDesc JSON file into a local buffer and returns
        /// flat instructions to recreate a single space.
        /// Does not modify the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpace(string filePath, string spaceName)
        {
            ValidateArgs(filePath, spaceName);

            var gd = DeserializeGameDescFile(filePath);
            if (gd == null) return null;

            var space = FindSpaceInDesc(gd, spaceName);
            if (space == null)
                throw new InvalidOperationException($"Space '{spaceName}' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        /// <summary>
        /// Loads a GameDesc JSON file into a local buffer and returns
        /// flat instructions for the specified spaces.
        /// If spaceNames is null or empty, returns instructions for ALL spaces.
        /// Does not modify the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string filePath, string[] spaceNames)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);

            var gd = DeserializeGameDescFile(filePath);
            if (gd == null) return null;

            if (spaceNames == null || spaceNames.Length == 0)
            {
                return GameDescParser.ParseForInstructions(gd);
            }

            var result = new List<Instruction>();
            foreach (var name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var space = FindSpaceInDesc(gd, name);
                if (space == null)
                {
                    UnityEngine.Debug.LogWarning($"Space '{name}' not found in GameDesc, skipping.");
                    continue;
                }
                var spaceInstructions = GameDescParser.ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Parses a comma-separated space list string.
        /// If null or empty, returns null (meaning "all spaces").
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string filePath, string spaceList = null)
        {
            if (string.IsNullOrWhiteSpace(spaceList))
            {
                return GetInstructionsForSpaces(filePath);
            }

            var names = spaceList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = names[i].Trim();
            }
            return GetInstructionsForSpaces(filePath, names);
        }

        // ============================================================
        // From current Snapshot
        // ============================================================

        /// <summary>
        /// Generates flat instructions for a single space from the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpace(string spaceName)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName);
            if (space == null)
                throw new InvalidOperationException($"Space '{spaceName}' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        /// <summary>
        /// Generates flat instructions for specified spaces from the current Snapshot.
        /// If spaceNames is null or empty, returns ALL spaces.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string[] spaceNames)
        {
            EnsureSnapshot();

            if (spaceNames == null || spaceNames.Length == 0)
            {
                return GameDescParser.ParseForInstructions(Snapshot);
            }

            var result = new List<Instruction>();
            foreach (var name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var space = FindSpace(name);
                if (space == null)
                {
                    UnityEngine.Debug.LogWarning($"Space '{name}' not found in Snapshot, skipping.");
                    continue;
                }
                var spaceInstructions = GameDescParser.ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Generates flat instructions for ALL spaces from the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces()
        {
            EnsureSnapshot();
            return GameDescParser.ParseForInstructions(Snapshot);
        }

        // ============================================================
        // Private helpers
        // ============================================================

        private static void ValidateArgs(string filePath, string spaceName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);
        }

        private GameDesc DeserializeGameDescFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var wrapper = JsonConvert.DeserializeObject<GameDescFile>(json, _jsonSettings);
            if (wrapper?.GameDesc == null)
                throw new InvalidOperationException("Invalid file: GameDesc missing.");

            var gd = wrapper.GameDesc;
            RebuildParents(gd);
            Normalize(gd);
            return gd;
        }

        private static Space FindSpaceInDesc(GameDesc gd, string spaceName)
        {
            return gd?.Spaces?.Find(s =>
                s != null && string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Parses a GameDesc into flat instruction lists for batch execution.
    /// Generates create_object, set_property, add_component, and attach_to
    /// instructions that can recreate any space from its hierarchical data.
    /// </summary>
    public class GameDescParser
    {
        // ============================================================
        // Entry points
        // ============================================================

        /// <summary>
        /// Parse all spaces in a GameDesc into a flat instruction array.
        /// </summary>
        public static bool ParseForInstructions(GameDesc gameDesc, out Instruction[] instructions)
        {
            instructions = ParseForInstructions(gameDesc);
            return instructions != null;
        }

        /// <summary>
        /// Parse all spaces in a GameDesc into a flat instruction array.
        /// </summary>
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
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        // ============================================================
        // Space → flat Instruction[]
        // ============================================================

        /// <summary>
        /// Parse a single space into a flat instruction list.
        /// Order: create_space, then each root object's subtree (DFS),
        /// then space-level components.
        /// </summary>
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

            // 2. Each root object's subtree
            foreach (var obj in space.Objects)
            {
                if (obj == null) continue;
                result.AddRange(ParseObjectForInstructions(obj));
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
        // Object → flat Instruction[] (subtree)
        // ============================================================

        /// <summary>
        /// Parse an object and its entire subtree into flat instructions.
        /// Order: create_object, set_property for each obj property,
        /// add_component for each component, then recursively for children,
        /// then attach_to for each child.
        /// </summary>
        public static Instruction[] ParseObjectForInstructions(T2G.Assistant.Object obj, string parentName = null, string socketName = null)
        {
            if (obj == null) return null;

            var result = new List<Instruction>();

            // 1. create_object (no Parent param — hierarchy built via attach_to)
            var createInstr = new Instruction
            {
                action = T2G.Actions.create_object,
                state = Instruction.eState.Resolved,
                desc = obj.Desc ?? string.Empty,
            };
            createInstr.parameters = new List<ValuePair>
            {
                new ValuePair("Name", obj.Name)
            };
            // Assets
            if (obj.Assets != null && obj.Assets.Count > 0)
            {
                createInstr.assets = new List<string>(obj.Assets);
            }
            result.Add(createInstr);

            if (!string.IsNullOrEmpty(parentName))
            {
                var attachInstr = ParseAttachParentInstruction(obj, parentName);
                if (attachInstr != null)
                {
                    result.Add(attachInstr);
                }
            }

            // 2. Object-level components
            if (obj.Components != null)
            {
                foreach (var comp in obj.Components)
                {
                    if (comp == null) continue;
                    result.Add(ParseComponentForInstructions(comp, obj.Name));
                }
            }

            // 3. Object-level properties → set_property
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

            // 4. Children (recursive, depth-first)
            for (int i = 0; i < (obj.Children?.Count ?? 0); i++)
            {
                var child = obj.Children[i];
                if (child == null)
                {
                    continue;
                }

                var childInstructions = ParseObjectForInstructions(child, obj.Name);
                if (childInstructions != null)
                {
                    result.AddRange(childInstructions);
                }
            }

            return result.ToArray();
        }

        static Instruction ParseAttachParentInstruction(T2G.Assistant.Object obj, string parentName)
        {
            var attachInstr = new Instruction
            {
                action = T2G.Actions.attach_to,
                state = Instruction.eState.Resolved
            };

            attachInstr.parameters = new List<ValuePair>
            {
                new ValuePair("source", obj.Name),
                new ValuePair("target", parentName)
            };

            if (!string.IsNullOrEmpty(obj.Socket))
            {
                attachInstr.parameters.Add(new ValuePair("socket", obj.Socket));
            }

            return attachInstr;
        }


        // ============================================================
        // Component → Instruction (add_component with nested set_property)
        // ============================================================

        /// <summary>
        /// Parse an object-level component into an add_component instruction
        /// with nested set_property sub-instructions for each property.
        /// </summary>
        public static Instruction ParseComponentForInstructions(T2G.Assistant.Component component, string objectName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = Instruction.eState.Resolved,
                assets = component.Assets
            };
            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", objectName),
                new ValuePair("type", component.Type ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            // Properties → nested set_property instructions
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
                        // Use "ComponentType.PropertyName" format — handles component-qualified properties
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

        /// <summary>
        /// Parse a space-level component (targets the space itself).
        /// </summary>
        public static Instruction ParseSpaceComponentForInstructions(T2G.Assistant.Component component, string spaceName)
        {
            if (component == null) return null;

            var instr = new Instruction
            {
                action = T2G.Actions.add_component,
                state = Instruction.eState.Resolved
            };
            instr.parameters = new List<ValuePair>
            {
                new ValuePair("objName", spaceName),
                new ValuePair("type", component.Type ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(component.Description))
            {
                instr.desc = component.Description;
            }

            // Properties → nested set_property
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
