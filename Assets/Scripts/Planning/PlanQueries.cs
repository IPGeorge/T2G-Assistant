using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// Read-only helpers for callers that consume an InstructionPlan. These
    /// are intended for the deferred retry queue: they answer "what is this
    /// instruction still waiting for?" and "what depends on it?".
    /// </summary>
    public static class PlanQueries
    {
        public static IReadOnlyList<string> GetDirectDependencies(
            InstructionPlan plan,
            string instructionId)
        {
            var result = new List<string>();

            if (plan == null || string.IsNullOrWhiteSpace(instructionId))
                return result;

            foreach (PlanEdge edge in plan.Edges)
            {
                if (string.Equals(edge.To, instructionId, StringComparison.Ordinal) &&
                    !result.Contains(edge.From))
                {
                    result.Add(edge.From);
                }
            }

            return result;
        }

        public static IReadOnlyList<string> GetDependents(
            InstructionPlan plan,
            string instructionId)
        {
            var result = new List<string>();

            if (plan == null || string.IsNullOrWhiteSpace(instructionId))
                return result;

            foreach (PlanEdge edge in plan.Edges)
            {
                if (string.Equals(edge.From, instructionId, StringComparison.Ordinal) &&
                    !result.Contains(edge.To))
                {
                    result.Add(edge.To);
                }
            }

            return result;
        }

        /// <summary>
        /// Direct dependencies that have not completed yet. The completed set
        /// may contain plan keys or instruction ids.
        /// </summary>
        public static IReadOnlyList<string> GetBlockingDependencies(
            InstructionPlan plan,
            string instructionId,
            ISet<string> completedIds)
        {
            var result = new List<string>();

            foreach (string dependency in GetDirectDependencies(plan, instructionId))
            {
                if (!IsCompleted(plan, dependency, completedIds))
                    result.Add(dependency);
            }

            return result;
        }

        public static bool IsDeferred(InstructionPlan plan, string instructionId)
        {
            if (plan == null || string.IsNullOrWhiteSpace(instructionId))
                return false;

            return plan.DeferredKeys.Contains(instructionId);
        }

        private static bool IsCompleted(
            InstructionPlan plan,
            string key,
            ISet<string> completedIds)
        {
            if (completedIds == null)
                return false;

            if (completedIds.Contains(key))
                return true;

            Instruction instruction;
            if (plan != null &&
                plan.InstructionByKey.TryGetValue(key, out instruction) &&
                instruction != null &&
                !string.IsNullOrWhiteSpace(instruction.id))
            {
                return completedIds.Contains(instruction.id);
            }

            return false;
        }
    }
}
