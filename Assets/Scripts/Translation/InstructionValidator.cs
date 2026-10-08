using System;
using System.Collections.Generic;
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
                        result.AddError($"The denpended local instruction {localToGlobalIdsMap[instId]} doesn't exist.");
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
                result.AddError(
                    $"Instruction {instruction.id}: translated instruction " +
                    $"cannot already be Resolved.");
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
            if (instruction.parameters == null || instruction.parameters.Count == 0)
            {
                if (actionMeta.parameters.Length > 0)
                {
                    result.AddError($"Instruction {instruction.id}: " + $"is missing required parameters!");
                }
                return;
            }

            foreach (string param in actionMeta.parameters)
            {
                string foundParameter = null;
                foreach (var parameter in instruction.parameters)
                {
                    if(string.Compare(parameter.name, param) == 0)
                    {
                        foundParameter = param;
                        break;
                    }
                }

                if(string.IsNullOrEmpty(foundParameter))
                {
                    result.AddError($"Instruction {instruction.id}: " + $"Missing required parameter {foundParameter}");
                }
            }
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

                if (string.IsNullOrWhiteSpace(asset.desc) &&
                    string.IsNullOrWhiteSpace(asset.source))
                {
                    result.AddError(
                        $"Instruction {instruction.id}: asset must " +
                        $"contain either desc or source.");
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