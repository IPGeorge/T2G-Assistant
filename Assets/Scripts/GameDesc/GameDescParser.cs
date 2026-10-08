using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using T2G;

namespace T2G.Assistant
{
    /// <summary>
    /// Converts persistent flat GameDesc state back into elemental instructions
    /// for regeneration/batch execution.
    /// </summary>
    public static class GameDescParser
    {
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
            if (gameDesc.Spaces == null)
                return result.ToArray();

            foreach (Space space in gameDesc.Spaces)
            {
                if (space == null) continue;
                Instruction[] items = ParseSpaceForInstructions(space);
                if (items != null) result.AddRange(items);
            }

            return result.ToArray();
        }

        public static Instruction[] ParseSpaceForInstructions(Space space)
        {
            if (space == null)
                return null;

            var result = new List<Instruction>();

            // 1. Create the space.
            result.Add(CreateInstruction(
                Actions.create_space,
                new Instruction.Parameter("SpaceName", "String", JToken.FromObject(space.Name ?? string.Empty))));

            // 2. Restore space-level properties.
            AddPropertyInstructions(result, space.Name, space.Properties, null);

            // 3. Restore space-level components and their properties.
            if (space.Components != null)
            {
                foreach (Component component in space.Components)
                    AddComponentInstructions(result, component, space.Name, space);
            }

            // Build GUID -> Name map for relationship targets.
            var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (space.Objects != null)
            {
                foreach (Object obj in space.Objects.Values)
                {
                    if (obj != null && !string.IsNullOrWhiteSpace(obj.Id) && !string.IsNullOrWhiteSpace(obj.Name))
                        guidToName[obj.Id] = obj.Name;
                }

                // 4. Create objects and restore object-local state.
                foreach (Object obj in space.Objects.Values)
                {
                    if (obj == null) continue;
                    result.AddRange(ParseObjectForInstructions(obj, space, guidToName));
                }
            }

            return result.ToArray();
        }

        public static Instruction[] ParseObjectForInstructions(
            Object obj,
            Space space,
            Dictionary<string, string> guidToName = null)
        {
            if (obj == null)
                return null;

            var result = new List<Instruction>();

            var create = new Instruction
            {
                action = Actions.create_object,
                state = InstructionState.Resolved,
                desc = obj.Desc ?? string.Empty,
                parameters = new List<Instruction.Parameter>()
            };

            create.parameters.Add(new Instruction.Parameter(
                "Name", "String", JToken.FromObject(obj.Name ?? string.Empty)));

            // Optional semantic metadata used by GameDesc regeneration.
            if (obj.Tags != null && obj.Tags.Count > 0)
                create.parameters.Add(new Instruction.Parameter(
                    "Tags", "String", JToken.FromObject(string.Join(",", obj.Tags))));

            if (obj.Roles != null && obj.Roles.Count > 0)
                create.parameters.Add(new Instruction.Parameter(
                    "Roles", "String", JToken.FromObject(string.Join(",", obj.Roles))));

            PropertyDesc position = FindProperty(obj.Properties, "Position");
            if (position != null && position.Value != null)
            {
                create.parameters.Add(new Instruction.Parameter(
                    "Position",
                    NormalizeInstructionType(position.Type, position.Value),
                    position.Value.DeepClone()));
            }

            // create_object is the correct place to restore object asset requirements.
            create.assets = BuildInstructionAssets(obj.Assets, space);
            result.Add(create);

            // Restore remaining object properties. Position is already supplied to create_object.
            AddPropertyInstructions(result, obj.Name, obj.Properties, "Position");

            // Restore components and their component properties.
            if (obj.Components != null)
            {
                foreach (Component component in obj.Components)
                    AddComponentInstructions(result, component, obj.Name, space);
            }

            // Restore semantic/structural relationships after the object exists.
            if (obj.Relationships != null)
            {
                foreach (Relationship relation in obj.Relationships)
                {
                    if (relation == null ||
                        string.IsNullOrWhiteSpace(relation.Type) ||
                        string.IsNullOrWhiteSpace(relation.Target))
                        continue;

                    string targetName;
                    if (guidToName == null || !guidToName.TryGetValue(relation.Target, out targetName))
                        targetName = relation.Target;

                    if (string.Equals(relation.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase))
                    {
                        var attach = CreateInstruction(
                            Actions.attach_to,
                            new Instruction.Parameter("Source", "ObjectRef", JToken.FromObject(obj.Name ?? string.Empty)),
                            new Instruction.Parameter("Target", "ObjectRef", JToken.FromObject(targetName ?? string.Empty)));
                        result.Add(attach);
                    }
                    else
                    {
                        var setRelation = CreateInstruction(
                            Actions.set_relationship,
                            new Instruction.Parameter("Source", "ObjectRef", JToken.FromObject(obj.Name ?? string.Empty)),
                            new Instruction.Parameter("Target", "ObjectRef", JToken.FromObject(targetName ?? string.Empty)),
                            new Instruction.Parameter("Type", "String", JToken.FromObject(relation.Type)),
                            new Instruction.Parameter("Slot", "String", JToken.FromObject(relation.Slot ?? string.Empty)));
                        result.Add(setRelation);
                    }
                }
            }

            return result.ToArray();
        }

        public static Instruction ParseComponentForInstructions(Component component, string ownerName)
        {
            if (component == null)
                return null;

            var instruction = CreateInstruction(
                Actions.add_component,
                new Instruction.Parameter("ObjName", "String", JToken.FromObject(ownerName ?? string.Empty)),
                new Instruction.Parameter("Type", "String", JToken.FromObject(component.Type ?? string.Empty)));

            instruction.desc = component.Description ?? string.Empty;
            return instruction;
        }

        public static Instruction ParseSpaceComponentForInstructions(Component component, string spaceName)
        {
            return ParseComponentForInstructions(component, spaceName);
        }

        private static void AddComponentInstructions(
            List<Instruction> result,
            Component component,
            string ownerName,
            Space space)
        {
            if (component == null)
                return;

            Instruction add = ParseComponentForInstructions(component, ownerName);
            add.assets = BuildInstructionAssets(component.Assets, space);
            result.Add(add);

            // Component properties use the established dotted-property convention.
            AddPropertyInstructions(result, ownerName, component.Properties, null, component.Type);
        }

        private static void AddPropertyInstructions(
            List<Instruction> result,
            string ownerName,
            List<PropertyDesc> properties,
            string skipProperty,
            string componentType = null)
        {
            if (properties == null)
                return;

            foreach (PropertyDesc property in properties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                    continue;

                if (!string.IsNullOrWhiteSpace(skipProperty) &&
                    string.Equals(property.Name, skipProperty, StringComparison.OrdinalIgnoreCase))
                    continue;

                string propertyName = string.IsNullOrWhiteSpace(componentType)
                    ? property.Name
                    : componentType + "." + property.Name;

                result.Add(CreateInstruction(
                    Actions.set_property,
                    new Instruction.Parameter("ObjName", "String", JToken.FromObject(ownerName ?? string.Empty)),
                    new Instruction.Parameter("Property", "String", JToken.FromObject(propertyName)),
                    new Instruction.Parameter(
                        "Value",
                        NormalizeInstructionType(property.Type, property.Value),
                        property.Value != null ? property.Value.DeepClone() : JValue.CreateNull())));
            }
        }

        private static List<Instruction.Asset> BuildInstructionAssets(List<AssetRef> refs, Space space)
        {
            var result = new List<Instruction.Asset>();
            if (refs == null || space == null || space.Assets == null)
                return result;

            foreach (AssetRef assetRef in refs)
            {
                if (assetRef == null || string.IsNullOrWhiteSpace(assetRef.AssetId))
                    continue;

                GameAsset asset;
                if (!space.Assets.TryGetValue(assetRef.AssetId, out asset) || asset == null)
                    continue;

                AssetType assetType;
                if (!Enum.TryParse(asset.Type, true, out assetType))
                    assetType = AssetType.Unknown;

                string source = !string.IsNullOrWhiteSpace(asset.Source)
                    ? asset.Source
                    : (!string.IsNullOrWhiteSpace(asset.ImportPath) ? asset.ImportPath : asset.LoadPath);

                result.Add(new Instruction.Asset
                {
                    desc = asset.Name ?? string.Empty,
                    type = assetType,
                    source = source ?? string.Empty
                });
            }

            return result;
        }

        private static PropertyDesc FindProperty(List<PropertyDesc> properties, string name)
        {
            if (properties == null)
                return null;

            foreach (PropertyDesc property in properties)
            {
                if (property != null &&
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property;
            }
            return null;
        }

        private static Instruction CreateInstruction(string action, params Instruction.Parameter[] parameters)
        {
            return new Instruction
            {
                action = action,
                state = InstructionState.Resolved,
                parameters = parameters != null
                    ? new List<Instruction.Parameter>(parameters)
                    : new List<Instruction.Parameter>()
            };
        }

        private static string NormalizeInstructionType(string declaredType, JToken value)
        {
            if (!string.IsNullOrWhiteSpace(declaredType))
                return declaredType;
            return value != null ? value.Type.ToString() : "Null";
        }
    }
}
