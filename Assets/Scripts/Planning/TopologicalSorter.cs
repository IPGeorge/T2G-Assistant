using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// Kahn's algorithm with a deterministic tie-break on the original list
    /// index. Independent instructions therefore keep the order in which the
    /// translator produced them.
    ///
    /// Nodes that cannot be placed are returned as deferred. Strongly
    /// connected components among the deferred nodes are reported separately
    /// so callers can distinguish a real cycle from an instruction that is
    /// merely blocked by one.
    /// </summary>
    internal static class TopologicalSorter
    {
        public static void Sort(
            DependencyGraph graph,
            List<string> ordered,
            List<string> deferred,
            List<List<string>> cycles)
        {
            if (graph == null)
                return;

            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var ready = new SortedSet<(int Index, string Key)>();

            foreach (var pair in graph.Nodes)
            {
                inDegree[pair.Key] = pair.Value.InDegree;

                if (pair.Value.InDegree == 0)
                    ready.Add((pair.Value.Index, pair.Key));
            }

            var placed = new HashSet<string>(StringComparer.Ordinal);

            while (ready.Count > 0)
            {
                (int Index, string Key) current = ready.Min;
                ready.Remove(current);

                ordered.Add(current.Key);
                placed.Add(current.Key);

                foreach (PlanEdge edge in graph.Nodes[current.Key].Outgoing)
                {
                    int remaining = --inDegree[edge.To];

                    if (remaining == 0 && !placed.Contains(edge.To))
                    {
                        DependencyGraph.Node target = graph.Nodes[edge.To];
                        ready.Add((target.Index, target.Key));
                    }
                }
            }

            var residual = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in graph.Nodes)
            {
                if (placed.Contains(pair.Key))
                    continue;

                residual.Add(pair.Key);
            }

            // Deferred nodes stay in their original relative order.
            var unplaced = new List<DependencyGraph.Node>();
            foreach (var pair in graph.Nodes)
            {
                if (residual.Contains(pair.Key))
                    unplaced.Add(pair.Value);
            }

            unplaced.Sort((a, b) => a.Index.CompareTo(b.Index));

            foreach (DependencyGraph.Node node in unplaced)
                deferred.Add(node.Key);

            foreach (List<string> component in FindStronglyConnectedComponents(graph, residual))
                cycles.Add(component);
        }

        /// <summary>
        /// Tarjan's algorithm restricted to the unplaced nodes. Only real
        /// cycles are returned (components with more than one node).
        /// </summary>
        private static List<List<string>> FindStronglyConnectedComponents(
            DependencyGraph graph,
            HashSet<string> residual)
        {
            var result = new List<List<string>>();

            if (residual.Count == 0)
                return result;

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            var lowLink = new Dictionary<string, int>(StringComparer.Ordinal);
            var onStack = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            int counter = 0;

            void Visit(string key)
            {
                index[key] = counter;
                lowLink[key] = counter;
                counter++;

                stack.Push(key);
                onStack.Add(key);

                foreach (PlanEdge edge in graph.Nodes[key].Outgoing)
                {
                    if (!residual.Contains(edge.To))
                        continue;

                    if (!index.ContainsKey(edge.To))
                    {
                        Visit(edge.To);
                        lowLink[key] = Math.Min(lowLink[key], lowLink[edge.To]);
                    }
                    else if (onStack.Contains(edge.To))
                    {
                        lowLink[key] = Math.Min(lowLink[key], index[edge.To]);
                    }
                }

                if (lowLink[key] != index[key])
                    return;

                var component = new List<string>();
                string member;

                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    component.Add(member);
                }
                while (!string.Equals(member, key, StringComparison.Ordinal));

                if (component.Count > 1)
                {
                    component.Sort((a, b) =>
                        graph.Nodes[a].Index.CompareTo(graph.Nodes[b].Index));
                    result.Add(component);
                }
            }

            foreach (string key in residual)
            {
                if (!index.ContainsKey(key))
                    Visit(key);
            }

            return result;
        }
    }
}
