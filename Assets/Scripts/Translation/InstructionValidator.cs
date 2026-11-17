using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;


namespace T2G.Assistant
{
    public class InstructionValidationResult
    {
        public bool IsValid => Errors.Count == 0;

        public List<string> Errors { get; } = new List<string>();

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
                Errors.Add(error);
        }

        public void Merge(InstructionValidationResult other)
        {
            if (other == null)
                return;

            Errors.AddRange(other.Errors);
        }
    }

    /// <summary>
    /// Validates normalized T2G instructions.
    /// Validation succeed promotes an instruction from Init state to Raw state.
    /// </summary>
    public static class InstructionValidator
    {
        private static readonly HashSet<string> CompositeActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                T2G.Actions.place_on
            };

            private static readonly HashSet<string> LocalActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                T2G.Actions.no_action,

                T2G.Actions.create_project,
                T2G.Actions.create_from,
                T2G.Actions.init_project,
                T2G.Actions.open_project,

                T2G.Actions.connect,
                T2G.Actions.disconnect,

                T2G.Actions.clear
            };


        private static readonly HashSet<string> KnownActions = BuildKnownActionSet();

        // Actions whose "Name" parameter refers to an object in the current space.
        // create_object is excluded because it defines a new object.
        private static readonly HashSet<string> ObjectNameActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                T2G.Actions.select_object,
                T2G.Actions.delete_object,
                T2G.Actions.place_on,
                T2G.Actions.detach_from,
                T2G.Actions.set_property,
                T2G.Actions.add_component,
                T2G.Actions.remove_component,
                T2G.Actions.call_method
            };

        // Actions whose "Source" and "Target" parameters both refer to objects.
        private static readonly HashSet<string> SourceTargetActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                T2G.Actions.attach_to,
                T2G.Actions.set_relationship
            };

        // Actions that accept the current space's name in addition to object names.
        private static readonly HashSet<string> SpaceNameActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                T2G.Actions.set_property,
                T2G.Actions.add_component,
                T2G.Actions.remove_component
            };

        public static InstructionValidationResult Validate(
            InstructionBase instructionBase, 
            Dictionary<string, string> localToGlobalIdsMap)
        {
            var result = new InstructionValidationResult();

            if (instructionBase == null)
            {
                result.AddError("Instruction is null.");
                return result;
            }

            if (instructionBase is InstructionSequence sequence)
            {
                ValidateSequence(sequence, localToGlobalIdsMap, result);
                return result;
            }

            if (instructionBase is Instruction instruction)
            {
                ValidateInstruction(instruction, localToGlobalIdsMap, result);

                if (result.IsValid)
                    instruction.state = InstructionState.Raw;

                return result;
            }

            result.AddError(
                $"Unsupported instruction class: " +
                $"{instructionBase.GetType().Name}");

            return result;
        }


        // ---------------------------------------------------------
        // Instruction
        // ---------------------------------------------------------
        private static void ValidateInstruction(
            Instruction instruction,
            Dictionary<string, string> localToGlobalIdsMap,
            InstructionValidationResult result)
        {
            ValidateStructure(instruction, result);

            if (!result.IsValid)
                return;

            ValidateAction(instruction, result);
            ValidateAssets(instruction, result);

            //Validate dependency instructions
            if (instruction.dependsOn != null && instruction.dependsOn.Count > 0)
            {
                List<string> globalDependenciesIds = new List<string>();
                for (int i = instruction.dependsOn.Count - 1; i >= 0; --i)
                {
                    var instId = instruction.dependsOn[i];
                    if (localToGlobalIdsMap.ContainsKey(instId))
                    {
                        globalDependenciesIds.Add(localToGlobalIdsMap[instId]);
                    }
                    else
                    {
                        result.AddError($"The depended instruction {instId} doesn't exist.");
                    }
                }
                instruction.dependsOn = globalDependenciesIds;
            }
        }


        // ---------------------------------------------------------
        // Basic structure
        // ---------------------------------------------------------

        private static void ValidateStructure(Instruction instruction, InstructionValidationResult result)
        {
            if (instruction == null)
            {
                result.AddError("Instruction is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(instruction.action))
            {
                result.AddError($"Instruction {instruction.id}: action is empty.");
            }

            if (instruction.type == InstructionType.Sequence)
            {
                result.AddError(
                    $"Instruction {instruction.id}: an Instruction cannot " +
                    $"have type Sequence. Use InstructionSequence.");
            }

            if (instruction.state == InstructionState.Resolved)
            {
                //result.AddError(
                //    $"Instruction {instruction.id}: translated instruction " +
                //    $"is already be Resolved.");
            }
        }


        // ---------------------------------------------------------
        // Action
        // ---------------------------------------------------------
        private static void ValidateAction(Instruction instruction, InstructionValidationResult result)
        {
            if (Actions.ActionMap.ContainsKey(instruction.action))
            {
                var actionMeta = Actions.ActionMap[instruction.action];
                instruction.type = actionMeta.instructionType;
                instruction.state = actionMeta.instructionState;
                ValidateParameters(instruction, actionMeta, result);
            }
            else
            {
                result.AddError($"Instruction {instruction.id}: unknown action " + $"'{instruction.action}'.");
            }
        }

        // ---------------------------------------------------------
        // Parameters
        // ---------------------------------------------------------
        private static void ValidateParameters(Instruction instruction, 
            Actions.ActionMeta actionMeta, 
            InstructionValidationResult result)
        {
            // A create_object whose object already exists in the current space
            // is already satisfied. Skip the required-parameter checks
            // (notably Position) so replaying a GameDesc batch does not fail
            // validation for objects that are already present. Execution
            // treats such an instruction as a successful no-op.
            if (string.Equals(
                instruction.action,
                Actions.create_object,
                StringComparison.OrdinalIgnoreCase))
            {
                string existingName = GetParameterValue(instruction, "Name");

                if (!string.IsNullOrWhiteSpace(existingName) &&
                    GameDescManager.Instance.FindObjectByName(existingName) != null)
                {
                    return;
                }
            }

            if (instruction.parameters == null || instruction.parameters.Count == 0)
            {
                if (actionMeta != null && actionMeta.parameters != null && actionMeta.parameters.Length > 0)
                {
                    result.AddError($"Instruction {instruction.id}: " + $"is missing required parameters!");
                }
                return;
            }

            if (actionMeta != null && actionMeta.parameters != null)
            {
                foreach (string param in actionMeta.parameters)
                {
                    if (string.IsNullOrWhiteSpace(GetParameterValue(instruction, param)))
                    {
                        result.AddError($"Instruction {instruction.id}: " + $"Missing required parameter {param}");
                    }
                }
            }

            if (!result.IsValid)
                return;

            ValidateObjectReferences(instruction, result);
        }

        /// <summary>
        /// Checks that every parameter that names an object (Name, Source,
        /// Target) refers to an object that already exists in the current
        /// space. Validation happens right before execution, so the target
        /// must exist at this point.
        /// </summary>
        private static void ValidateObjectReferences(
            Instruction instruction,
            InstructionValidationResult result)
        {
            string action = instruction.action ?? string.Empty;
            List<string> references = new List<string>();

            if (SourceTargetActions.Contains(action))
            {
                AddObjectReference(instruction, "Source", references);
                AddObjectReference(instruction, "Target", references);
            }
            else if (ObjectNameActions.Contains(action))
            {
                AddObjectReference(instruction, "Name", references);
            }
            else
            {
                return;
            }

            if (references.Count == 0)
                return;

            GameDescManager manager = GameDescManager.Instance;
            Space currentSpace = manager.CurrentSpace;
            bool allowSpaceName = SpaceNameActions.Contains(action);

            foreach (string objectName in references)
            {
                if (manager.FindObjectByName(objectName) != null)
                    continue;

                if (allowSpaceName &&
                    currentSpace != null &&
                    string.Equals(
                        currentSpace.Name,
                        objectName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (currentSpace == null)
                {
                    result.AddError(
                        $"Instruction {instruction.id}: cannot resolve " +
                        $"'{objectName}' because there is no current space.");
                }
                else
                {
                    result.AddError(
                        $"Instruction {instruction.id}: referenced object " +
                        $"'{objectName}' does not exist in space '{currentSpace.Name}'.");
                }
            }
        }

        private static void AddObjectReference(
            Instruction instruction,
            string parameterName,
            List<string> references)
        {
            string value = GetParameterValue(instruction, parameterName);

            if (!string.IsNullOrWhiteSpace(value) &&
                !references.Exists(r => string.Equals(r, value, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(value);
            }
        }

        private static string GetParameterValue(Instruction instruction, string name)
        {
            if (instruction == null ||
                instruction.parameters == null ||
                string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (var parameter in instruction.parameters)
            {
                if (parameter == null || parameter.value == null)
                    continue;

                if (!string.Equals(parameter.name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (parameter.value.Type == Newtonsoft.Json.Linq.JTokenType.Null ||
                    parameter.value.Type == Newtonsoft.Json.Linq.JTokenType.Undefined)
                {
                    return null;
                }

                return parameter.value.ToString();
            }

            return null;
        }

        // ---------------------------------------------------------
        // Assets
        // ---------------------------------------------------------

        private static void ValidateAssets(
            Instruction instruction,
            InstructionValidationResult result)
        {
            if (instruction.assets == null)
                return;

            foreach (var asset in instruction.assets)
            {
                if (asset == null)
                {
                    result.AddError(
                        $"Instruction {instruction.id}: " +
                        $"contains a null asset.");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(asset.desc) && string.IsNullOrWhiteSpace(asset.source))
                {
                    result.AddError(
                        $"Instruction {instruction.id}: asset must " +
                        $"contain either desc or source.");
                }

                //Validate asset type
                asset.type = AssetType.SourceAsset;  //Needed  to be resolved  
                if (!string.IsNullOrWhiteSpace(asset.desc))
                {
                    if (T2G.Utils.IsNodeObjectDesc(asset.desc))
                    {
                        asset.type = AssetType.Node;
                        asset.source = asset.desc;
                    }
                    else if (T2G.Utils.IsCameraDesc(asset.desc) ||
                        T2G.Utils.IsLightDesc(asset.desc) ||
                        T2G.Utils.IsPrimitiveDesc(asset.desc, out var primitiveType) ||
                        T2G.Utils.IsValidComponentName(asset.desc))
                    {
                        asset.type = AssetType.Identifier;
                    }

                    if (!string.IsNullOrWhiteSpace(asset.source))
                    {
                        if(T2G.Utils.IsScript(asset.source))
                        {
                            asset.type = AssetType.Script;
                        }
                        else if(File.Exists(asset.source))
                        {
                            asset.type = AssetType.ScriptFile;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // Sequence
        // ---------------------------------------------------------

        private static void ValidateSequence(
            InstructionSequence sequence,
            Dictionary<string, string> localToGlobalIdsMap,
            InstructionValidationResult result)
        {
            if (sequence.type != InstructionType.Sequence)
            {
                result.AddError($"InstructionSequence {sequence.id} must have " +$"InstructionType.Sequence.");
            }

            if (sequence.instructions == null ||
                sequence.instructions.Count == 0)
            {
                result.AddError($"InstructionSequence {sequence.id} contains " + $"no instructions.");
                return;
            }

            for (int i = 0; i < sequence.instructions.Count; i++)
            {
                InstructionBase child = sequence.instructions[i];

                var childResult = Validate(child, localToGlobalIdsMap);

                if (!childResult.IsValid)
                {
                    foreach (string error in childResult.Errors)
                    {
                        result.AddError($"Sequence[{i}]: {error}");
                    }
                }
            }
        }


        // ---------------------------------------------------------
        // Action discovery
        // ---------------------------------------------------------
        private static HashSet<string> BuildKnownActionSet()
        {
            var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            FieldInfo[] fields = typeof(T2G.Actions).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string))
                    continue;

                string value = field.GetValue(null) as string;

                if (!string.IsNullOrWhiteSpace(value))
                    actions.Add(value);
            }

            return actions;
        }
    }
}