using System;
using Newtonsoft.Json.Linq;

namespace T2G.Assistant
{
    /// <summary>
    /// Canonicalizes an InstructionBase after translation/deserialization.
    /// </summary>
    public static class InstructionNormalizer
    {
        public static InstructionBase Normalize(InstructionBase instructionBase)
        {
            if (instructionBase == null)
                return null;

            switch (instructionBase)
            {
                case InstructionSequence sequence:
                    return NormalizeSequence(sequence);

                case Instruction instruction:
                    return NormalizeInstruction(instruction);

                default:
                    return instructionBase;
            }
        }

        private static Instruction NormalizeInstruction(Instruction instruction)
        {
            // A translated instruction has not yet passed validation.
            instruction.state = InstructionState.Init;

            instruction.action = NormalizeString(instruction.action);
            instruction.desc = NormalizeString(instruction.desc);

            // Normalize parameters without changing their semantic values.
            if (instruction.parameters != null)
            {
                foreach (var parameter in instruction.parameters)
                {
                    if (parameter == null)
                        continue;

                    parameter.name = NormalizeString(parameter.name);
                    parameter.type = NormalizeString(parameter.type);

                    NormalizeParameterValue(parameter);
                }
            }

            // Normalize asset metadata only.
            if (instruction.assets != null)
            {
                foreach (var asset in instruction.assets)
                {
                    if (asset == null)
                        continue;

                    asset.desc = NormalizeString(asset.desc);
                    asset.source = NormalizeString(asset.source);
                }
            }

            return instruction;
        }

        private static InstructionSequence NormalizeSequence(InstructionSequence sequence)
        {
            sequence.desc = NormalizeString(sequence.desc);

            if (sequence.instructions != null)
            {
                for (int i = 0; i < sequence.instructions.Count; i++)
                {
                    sequence.instructions[i] = NormalizeInstruction(sequence.instructions[i]);
                }
            }

            return sequence;
        }

        private static void NormalizeParameterValue(
            Instruction.Parameter parameter)
        {
            if (parameter.value == null)
                return;

            // Only trim string values.
            if (parameter.value.Type == JTokenType.String)
            {
                string value = parameter.value.Value<string>();

                if (value != null)
                    parameter.value = value.Trim();
            }
        }

        private static string NormalizeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}