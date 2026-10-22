using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using T2G;

namespace T2G.Assistant
{
    /// <summary>
    /// Derives advisory ordering edges from action semantics.
    ///
    /// The rules run in two passes:
    ///  - forward: an edge is added when the producer appears before the
    ///    consumer, so references map to the definition currently in effect;
    ///  - declared-later: a reference with no earlier definition is linked to
    ///    a unique create_object that appears later in the batch, which lets
    ///    the planner move that definition ahead of its consumer.
    ///
    /// References without a definition anywhere in the batch are left alone,
    /// because they may refer to objects that already exist in the scene.
    /// delete_object and select_object are excluded from the second pass so a
    /// later creation is never moved ahead of an intended lifetime change.
    /// </summary>
    internal static class InstructionDependencyRules
    {
        public static void Apply(
            string[] keys,
            IReadOnlyList<Instruction> instructions,
            DependencyGraph graph,
            List<PlanDiagnostic> diagnostics)
        {
            if (keys == null || instructions == null || graph == null)
                return;

            // Object name -> key of the create_object currently defining it.
            var objectDefinitions =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Object name -> keys of instructions referencing it since the
            // current definition. Used to order delete_object after its uses.
            var objectUses =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Space name -> key of the create_space currently defining it.
            var spaceDefinitions =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Object name -> key of the latest add_component on that object.
            var componentDefinitions =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Object name -> key of the latest select_object.
            var selections =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < instructions.Count && i < keys.Length; i++)
            {
                Instruction instruction = instructions[i];
                string key = keys[i];

                if (instruction == null || string.IsNullOrEmpty(key))
                    continue;

                string action = instruction.action ?? string.Empty;

                if (Is(action, Actions.create_object))
                {
                    string name = GetParameter(instruction, "Name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        objectDefinitions[name] = key;
                        objectUses[name] = new List<string>();
                    }
                    continue;
                }

                if (Is(action, Actions.create_space))
                {
                    string name = GetParameter(instruction, "SpaceName");
                    if (!string.IsNullOrWhiteSpace(name))
                        spaceDefinitions[name] = key;
                    continue;
                }

                if (Is(action, Actions.goto_space) ||
                    Is(action, Actions.rename_space))
                {
                    string name = GetParameter(instruction, "SpaceName");
                    LinkProducer(
                        spaceDefinitions,
                        name,
                        key,
                        $"create_space({name}) -> {action}",
                        graph);
                    continue;
                }

                if (Is(action, Actions.delete_object))
                {
                    ApplyDelete(
                        instruction,
                        key,
                        graph,
                        objectDefinitions,
                        objectUses,
                        componentDefinitions,
                        selections);
                    continue;
                }

                if (Is(action, Actions.select_object))
                {
                    string name = GetParameter(instruction, "Name");
                    LinkObjectUse(
                        name,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);
                    if (!string.IsNullOrWhiteSpace(name))
                        selections[name] = key;
                    continue;
                }

                if (Is(action, Actions.add_component))
                {
                    string name = GetParameter(instruction, "Name");
                    LinkObjectUse(
                        name,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);

                    if (!string.IsNullOrWhiteSpace(name))
                        componentDefinitions[name] = key;
                    continue;
                }

                if (Is(action, Actions.set_property) ||
                    Is(action, Actions.call_method) ||
                    Is(action, Actions.remove_component))
                {
                    string name = GetParameter(instruction, "Name");
                    LinkObjectUse(
                        name,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);

                    LinkProducer(
                        componentDefinitions,
                        name,
                        key,
                        $"add_component({name}) -> {action}",
                        graph);

                    LinkProducer(
                        selections,
                        name,
                        key,
                        $"select_object({name}) -> {action}",
                        graph);
                    continue;
                }

                if (Is(action, Actions.place_on) ||
                    Is(action, Actions.detach_from))
                {
                    string name = GetParameter(instruction, "Name");
                    LinkObjectUse(
                        name,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);

                    LinkProducer(
                        selections,
                        name,
                        key,
                        $"select_object({name}) -> {action}",
                        graph);
                    continue;
                }

                if (Is(action, Actions.attach_to) ||
                    Is(action, Actions.set_relationship))
                {
                    string source = GetParameter(instruction, "Source");
                    string target = GetParameter(instruction, "Target");

                    LinkObjectUse(
                        source,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);
                    LinkObjectUse(
                        target,
                        key,
                        action,
                        graph,
                        objectDefinitions,
                        objectUses);

                    LinkProducer(
                        selections,
                        source,
                        key,
                        $"select_object({source}) -> {action}",
                        graph);
                    continue;
                }
            }

            // A batch generated from a GameDesc emits each object's
            // relationships inside that object's group. The target may
            // therefore be created later in the batch. Link such references
            // to a unique later definition so the planner can move the
            // definition ahead of the consumer.
            ApplyDeclaredLaterEdges(keys, instructions, graph, diagnostics);
        }

        private static void ApplyDeclaredLaterEdges(
            string[] keys,
            IReadOnlyList<Instruction> instructions,
            DependencyGraph graph,
            List<PlanDiagnostic> diagnostics)
        {
            if (keys == null || instructions == null || graph == null)
                return;

            var definitions =
                new Dictionary<string, List<(int Index, string Key)>>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < instructions.Count && i < keys.Length; i++)
            {
                Instruction instruction = instructions[i];

                if (instruction == null ||
                    string.IsNullOrEmpty(keys[i]) ||
                    !Is(instruction.action, Actions.create_object))
                {
                    continue;
                }

                string name = GetParameter(instruction, "Name");

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                List<(int Index, string Key)> entries;

                if (!definitions.TryGetValue(name, out entries))
                {
                    entries = new List<(int Index, string Key)>();
                    definitions.Add(name, entries);
                }

                entries.Add((i, keys[i]));
            }

            if (definitions.Count == 0)
                return;

            for (int i = 0; i < instructions.Count && i < keys.Length; i++)
            {
                Instruction instruction = instructions[i];

                if (instruction == null || string.IsNullOrEmpty(keys[i]))
                    continue;

                string action = instruction.action ?? string.Empty;

                // delete_object and select_object are deliberately excluded:
                // they may legitimately refer to an object from an earlier
                // batch, and moving a later creation ahead of them would
                // change the intended lifetime.
                foreach (string name in GetObjectReferences(instruction))
                {
                    List<(int Index, string Key)> entries;

                    if (!definitions.TryGetValue(name, out entries))
                        continue;

                    bool hasEarlierDefinition = false;

                    foreach ((int Index, string Key) entry in entries)
                    {
                        if (entry.Index < i)
                        {
                            hasEarlierDefinition = true;
                            break;
                        }
                    }

                    if (hasEarlierDefinition)
                        continue;

                    string laterKey = null;
                    int laterCount = 0;

                    foreach ((int Index, string Key) entry in entries)
                    {
                        if (entry.Index > i)
                        {
                            laterCount++;
                            laterKey = entry.Key;
                        }
                    }

                    if (laterCount == 1)
                    {
                        graph.AddEdge(
                            laterKey,
                            keys[i],
                            PlanEdgeKind.Implicit,
                            $"create_object({name}) declared later -> {action}");
                    }
                    else if (laterCount > 1)
                    {
                        diagnostics.Add(new PlanDiagnostic(
                            PlanDiagnosticSeverity.Info,
                            keys[i],
                            $"Reference '{name}' matches multiple later " +
                            "create_object definitions; order was left unchanged."));
                    }
                }
            }
        }

        private static IEnumerable<string> GetObjectReferences(
            Instruction instruction)
        {
            string action = instruction.action ?? string.Empty;

            if (Is(action, Actions.attach_to) ||
                Is(action, Actions.set_relationship))
            {
                string source = GetParameter(instruction, "Source");
                string target = GetParameter(instruction, "Target");

                if (!string.IsNullOrWhiteSpace(source))
                    yield return source;

                if (!string.IsNullOrWhiteSpace(target))
                    yield return target;

                yield break;
            }

            if (Is(action, Actions.add_component) ||
                Is(action, Actions.set_property) ||
                Is(action, Actions.call_method) ||
                Is(action, Actions.remove_component) ||
                Is(action, Actions.place_on) ||
                Is(action, Actions.detach_from))
            {
                string name = GetParameter(instruction, "Name");

                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
            }
        }

        private static void ApplyDelete(
            Instruction instruction,
            string key,
            DependencyGraph graph,
            Dictionary<string, string> objectDefinitions,
            Dictionary<string, List<string>> objectUses,
            Dictionary<string, string> componentDefinitions,
            Dictionary<string, string> selections)
        {
            string name = GetParameter(instruction, "Name");

            if (string.IsNullOrWhiteSpace(name))
                return;

            string definition;
            if (objectDefinitions.TryGetValue(name, out definition))
            {
                graph.AddEdge(
                    definition,
                    key,
                    PlanEdgeKind.Implicit,
                    $"create_object({name}) -> delete_object");
            }

            List<string> uses;
            if (objectUses.TryGetValue(name, out uses))
            {
                foreach (string use in uses)
                {
                    graph.AddEdge(
                        use,
                        key,
                        PlanEdgeKind.Implicit,
                        $"use({name}) -> delete_object");
                }
            }

            string component;
            if (componentDefinitions.TryGetValue(name, out component))
            {
                graph.AddEdge(
                    component,
                    key,
                    PlanEdgeKind.Implicit,
                    $"add_component({name}) -> delete_object");
            }

            string selection;
            if (selections.TryGetValue(name, out selection))
            {
                graph.AddEdge(
                    selection,
                    key,
                    PlanEdgeKind.Implicit,
                    $"select_object({name}) -> delete_object");
            }

            // A new definition after the delete starts a fresh lifetime.
            objectDefinitions.Remove(name);
            objectUses.Remove(name);
            componentDefinitions.Remove(name);
            selections.Remove(name);
        }

        private static void LinkObjectUse(
            string name,
            string key,
            string action,
            DependencyGraph graph,
            Dictionary<string, string> objectDefinitions,
            Dictionary<string, List<string>> objectUses)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            string definition;
            if (objectDefinitions.TryGetValue(name, out definition))
            {
                graph.AddEdge(
                    definition,
                    key,
                    PlanEdgeKind.Implicit,
                    $"create_object({name}) -> {action}");
            }

            List<string> uses;
            if (!objectUses.TryGetValue(name, out uses))
            {
                uses = new List<string>();
                objectUses.Add(name, uses);
            }

            if (!uses.Contains(key))
                uses.Add(key);
        }

        private static void LinkProducer(
            Dictionary<string, string> definitions,
            string name,
            string key,
            string reason,
            DependencyGraph graph)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            string producer;
            if (!definitions.TryGetValue(name, out producer))
                return;

            graph.AddEdge(producer, key, PlanEdgeKind.Implicit, reason);
        }

        private static bool Is(string action, string expected)
        {
            return string.Equals(action, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetParameter(Instruction instruction, string name)
        {
            if (instruction == null ||
                instruction.parameters == null ||
                string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (Instruction.Parameter parameter in instruction.parameters)
            {
                if (parameter == null || parameter.value == null)
                    continue;

                if (!string.Equals(parameter.name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (parameter.value.Type == JTokenType.Null ||
                    parameter.value.Type == JTokenType.Undefined)
                {
                    return null;
                }

                return parameter.value.ToString().Trim();
            }

            return null;
        }
    }
}
