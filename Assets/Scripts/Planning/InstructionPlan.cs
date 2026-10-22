using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// How a dependency edge was established.
    /// </summary>
    public enum PlanEdgeKind
    {
        Explicit = 0,   // Declared through Instruction.dependsOn.
        Implicit = 1    // Derived from action/parameter relationships.
    }

    /// <summary>
    /// A directed dependency edge: From must complete before To.
    /// </summary>
    public sealed class PlanEdge
    {
        public string From;
        public string To;
        public PlanEdgeKind Kind;
        public string Reason;

        public PlanEdge(string from, string to, PlanEdgeKind kind, string reason)
        {
            From = from;
            To = to;
            Kind = kind;
            Reason = reason ?? string.Empty;
        }

        public override string ToString()
        {
            return $"{From} -> {To} [{Kind}] {Reason}";
        }
    }

    public enum PlanDiagnosticSeverity
    {
        Info = 0,
        Warning = 1
    }

    /// <summary>
    /// A non-fatal note produced while building the plan. The planner never
    /// rejects a batch; callers decide what to do with the diagnostics.
    /// </summary>
    public sealed class PlanDiagnostic
    {
        public PlanDiagnosticSeverity Severity;
        public string InstructionId;
        public string Message;

        public PlanDiagnostic(
            PlanDiagnosticSeverity severity,
            string instructionId,
            string message)
        {
            Severity = severity;
            InstructionId = instructionId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(InstructionId)
                ? $"[{Severity}] {Message}"
                : $"[{Severity}] {InstructionId}: {Message}";
        }
    }

    /// <summary>
    /// Result of planning a batch of instructions.
    ///
    /// Ordered  : instructions that could be sorted topologically.
    /// Deferred : instructions that could not be placed (dependency cycles,
    ///            or instructions blocked by a cycle). They are returned in
    ///            their original relative order.
    /// </summary>
    public sealed class InstructionPlan
    {
        public List<Instruction> Ordered = new List<Instruction>();
        public List<Instruction> Deferred = new List<Instruction>();

        public List<PlanEdge> Edges = new List<PlanEdge>();
        public List<PlanDiagnostic> Diagnostics = new List<PlanDiagnostic>();

        // Node key -> position in the original input list.
        public Dictionary<string, int> OriginalIndexById =
            new Dictionary<string, int>(StringComparer.Ordinal);

        // Node key -> instruction. A key equals Instruction.id unless the id
        // was missing or duplicated, in which case "@plan:<index>" is used.
        public Dictionary<string, Instruction> InstructionByKey =
            new Dictionary<string, Instruction>(StringComparer.Ordinal);

        public List<string> DeferredKeys = new List<string>();

        public bool IsEmpty => Ordered.Count == 0 && Deferred.Count == 0;
        public bool HasDeferred => Deferred.Count > 0;

        public bool HasWarnings
        {
            get
            {
                foreach (var diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == PlanDiagnosticSeverity.Warning)
                        return true;
                }
                return false;
            }
        }

        public int GetOriginalIndex(string key)
        {
            int index;
            return key != null && OriginalIndexById.TryGetValue(key, out index)
                ? index
                : int.MaxValue;
        }
    }

    public sealed class PlanOptions
    {
        /// <summary>
        /// Adds ordering hints derived from action/parameter relationships.
        /// These edges are advisory; the planner never fails because of them.
        /// </summary>
        public bool EnableImplicitDependencies = true;
    }
}
