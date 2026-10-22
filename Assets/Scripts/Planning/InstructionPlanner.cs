using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// Builds an explicit dependency graph for a batch of instructions and
    /// returns a deterministic topological order.
    ///
    /// The planner never rejects a batch. Instructions that cannot be placed
    /// (dependency cycles) are returned through InstructionPlan.Deferred so a
    /// caller-owned retry queue can decide how to proceed.
    /// </summary>
    public static class InstructionPlanner
    {
        public static InstructionPlan BuildPlan(
            IReadOnlyList<Instruction> instructions,
            PlanOptions options = null)
        {
            options ??= new PlanOptions();

            var plan = new InstructionPlan();

            if (instructions == null || instructions.Count == 0)
                return plan;

            var graph = new DependencyGraph();
            var keys = new string[instructions.Count];
            var usedKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];

                if (instruction == null)
                {
                    plan.Diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticSeverity.Warning,
                        string.Empty,
                        $"Instruction at index {i} is null and was skipped."));
                    continue;
                }

                string key = instruction.id;

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = $"@plan:{i}";
                    plan.Diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticSeverity.Warning,
                        string.Empty,
                        $"Instruction at index {i} ({instruction.action}) has no id; " +
                        $"using '{key}'."));
                }
                else if (usedKeys.Contains(key))
                {
                    string fallback = $"@plan:{i}";
                    plan.Diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticSeverity.Warning,
                        key,
                        $"Duplicate instruction id at index {i} ({instruction.action}); " +
                        $"using '{fallback}'."));
                    key = fallback;
                }

                usedKeys.Add(key);
                keys[i] = key;

                graph.AddNode(key, instruction, i);
                plan.OriginalIndexById[key] = i;
                plan.InstructionByKey[key] = instruction;
            }

            AddExplicitEdges(keys, instructions, graph, plan);

            if (options.EnableImplicitDependencies)
            {
                InstructionDependencyRules.Apply(
                    keys,
                    instructions,
                    graph,
                    plan.Diagnostics);
            }

            var orderedKeys = new List<string>();
            var deferredKeys = new List<string>();
            var cycles = new List<List<string>>();

            TopologicalSorter.Sort(graph, orderedKeys, deferredKeys, cycles);

            foreach (string key in orderedKeys)
                plan.Ordered.Add(graph.Nodes[key].Instruction);

            foreach (string key in deferredKeys)
            {
                plan.DeferredKeys.Add(key);
                plan.Deferred.Add(graph.Nodes[key].Instruction);
            }

            foreach (PlanEdge edge in graph.AllEdges)
                plan.Edges.Add(edge);

            AddCycleDiagnostics(plan, graph, cycles);
            AddBlockedDiagnostics(plan, deferredKeys, cycles);

            return plan;
        }

        private static void AddExplicitEdges(
            string[] keys,
            IReadOnlyList<Instruction> instructions,
            DependencyGraph graph,
            InstructionPlan plan)
        {
            for (int i = 0; i < instructions.Count && i < keys.Length; i++)
            {
                Instruction instruction = instructions[i];
                string key = keys[i];

                if (instruction == null ||
                    string.IsNullOrEmpty(key) ||
                    instruction.dependsOn == null)
                {
                    continue;
                }

                foreach (string dependency in instruction.dependsOn)
                {
                    if (string.IsNullOrWhiteSpace(dependency))
                        continue;

                    if (string.Equals(dependency, key, StringComparison.Ordinal))
                    {
                        plan.Diagnostics.Add(new PlanDiagnostic(
                            PlanDiagnosticSeverity.Warning,
                            key,
                            "Instruction depends on itself; the edge was ignored."));
                        continue;
                    }

                    if (!graph.Contains(dependency))
                    {
                        plan.Diagnostics.Add(new PlanDiagnostic(
                            PlanDiagnosticSeverity.Warning,
                            key,
                            $"Depends on unknown instruction '{dependency}'; " +
                            "the edge was ignored."));
                        continue;
                    }

                    graph.AddEdge(
                        dependency,
                        key,
                        PlanEdgeKind.Explicit,
                        "dependsOn");
                }
            }
        }

        private static void AddCycleDiagnostics(
            InstructionPlan plan,
            DependencyGraph graph,
            List<List<string>> cycles)
        {
            foreach (List<string> component in cycles)
            {
                var parts = new List<string>();

                foreach (string key in component)
                {
                    Instruction instruction = graph.Nodes[key].Instruction;
                    parts.Add($"{key} ({instruction.action})");
                }

                plan.Diagnostics.Add(new PlanDiagnostic(
                    PlanDiagnosticSeverity.Warning,
                    component.Count > 0 ? component[0] : string.Empty,
                    "Dependency cycle detected: " +
                    string.Join(", ", parts) +
                    ". These instructions were deferred."));
            }
        }

        private static void AddBlockedDiagnostics(
            InstructionPlan plan,
            List<string> deferredKeys,
            List<List<string>> cycles)
        {
            var cyclic = new HashSet<string>(StringComparer.Ordinal);

            foreach (List<string> component in cycles)
            {
                foreach (string key in component)
                    cyclic.Add(key);
            }

            foreach (string key in deferredKeys)
            {
                if (cyclic.Contains(key))
                    continue;

                plan.Diagnostics.Add(new PlanDiagnostic(
                    PlanDiagnosticSeverity.Warning,
                    key,
                    "Deferred because it depends on a dependency cycle."));
            }
        }
    }
}
