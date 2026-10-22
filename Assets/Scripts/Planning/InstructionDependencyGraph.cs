using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// Directed dependency graph used by <see cref="InstructionPlanner"/>.
    /// Nodes are keyed by instruction id (or a synthetic key when the id is
    /// missing/duplicated). Only this planner reads the graph, so it stays
    /// internal.
    /// </summary>
    internal sealed class DependencyGraph
    {
        internal sealed class Node
        {
            public string Key;
            public Instruction Instruction;
            public int Index;
            public int InDegree;
            public readonly List<PlanEdge> Outgoing = new List<PlanEdge>();
        }

        private readonly Dictionary<string, Node> _nodes =
            new Dictionary<string, Node>(StringComparer.Ordinal);

        // Tracks existing edges so the same pair is only added once.
        private readonly Dictionary<string, HashSet<string>> _edges =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, Node> Nodes => _nodes;
        public int Count => _nodes.Count;

        public void AddNode(string key, Instruction instruction, int index)
        {
            if (string.IsNullOrEmpty(key) || _nodes.ContainsKey(key))
                return;

            _nodes.Add(key, new Node
            {
                Key = key,
                Instruction = instruction,
                Index = index,
                InDegree = 0
            });
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && _nodes.ContainsKey(key);
        }

        /// <summary>
        /// Adds From -> To. Returns false when the edge already exists, when
        /// either endpoint is unknown, or when it would be a self-loop.
        /// An existing implicit edge is upgraded to explicit when the same
        /// pair is also declared through dependsOn.
        /// </summary>
        public bool AddEdge(
            string from,
            string to,
            PlanEdgeKind kind,
            string reason)
        {
            if (!Contains(from) || !Contains(to) || from == to)
                return false;

            HashSet<string> targets;
            if (!_edges.TryGetValue(from, out targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                _edges.Add(from, targets);
            }

            if (!targets.Add(to))
            {
                if (kind == PlanEdgeKind.Explicit)
                {
                    PlanEdge existing = _nodes[from].Outgoing.Find(e => e.To == to);
                    if (existing != null)
                    {
                        existing.Kind = PlanEdgeKind.Explicit;
                        existing.Reason = reason ?? existing.Reason;
                    }
                }
                return false;
            }

            _nodes[from].Outgoing.Add(
                new PlanEdge(from, to, kind, reason));
            _nodes[to].InDegree++;
            return true;
        }

        public IEnumerable<PlanEdge> AllEdges
        {
            get
            {
                foreach (Node node in _nodes.Values)
                {
                    foreach (PlanEdge edge in node.Outgoing)
                        yield return edge;
                }
            }
        }
    }
}
