using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace T2G.Assistant
{
    /// <summary>
    /// Lightweight self-check for the planning module.
    ///
    /// The planner lives in Assembly-CSharp (no asmdef), which cannot be
    /// referenced by an EditMode test assembly. This editor-only runner keeps
    /// the checks available without adding an assembly definition.
    /// </summary>
    public static class InstructionPlannerSelfCheck
    {
        private static int s_caseCount;

        [MenuItem("Tools/T2G/Planning/Run Self-Check")]
        public static void Run()
        {
            var failures = new List<string>();
            s_caseCount = 0;

            EmptyPlan(failures);
            StableOrder(failures);
            ExplicitChainReorders(failures);
            ExplicitDiamondTieBreak(failures);
            CycleIsDeferred(failures);
            BlockedByCycleIsDeferred(failures);
            ImplicitCreateBeforeUse(failures);
            ImplicitDeclaredLater(failures);
            DeleteAfterUses(failures);
            SpaceOrder(failures);
            SelectBarrier(failures);
            AttachReferences(failures);
            AttachToLaterDefinition(failures);
            DanglingExplicitDependency(failures);
            SelfDependency(failures);
            DuplicateIds(failures);
            NullInstruction(failures);

            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[InstructionPlanner] Self-check passed ({s_caseCount} cases).");
                return;
            }

            Debug.LogError(
                $"[InstructionPlanner] Self-check failed " +
                $"({failures.Count}/{s_caseCount} cases):\n - " +
                string.Join("\n - ", failures));
        }

        // -----------------------------------------------------------------
        // Cases
        // -----------------------------------------------------------------

        private static void EmptyPlan(List<string> failures)
        {
            var plan = InstructionPlanner.BuildPlan(null);

            Expect(plan.IsEmpty, "empty input returns an empty plan", failures);
            Expect(plan.Diagnostics.Count == 0,
                "empty input produces no diagnostics", failures);
        }

        private static void StableOrder(List<string> failures)
        {
            var plan = Build(
                Make("1", Actions.no_action),
                Make("2", Actions.no_action),
                Make("3", Actions.no_action));

            Expect(Ids(plan.Ordered) == "1,2,3",
                "independent instructions keep their order", failures);
            Expect(!plan.HasDeferred,
                "independent instructions are not deferred", failures);
        }

        private static void ExplicitChainReorders(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            var b = Make("B", Actions.no_action);
            var c = Make("C", Actions.no_action);

            a.dependsOn.Add("B");
            b.dependsOn.Add("C");

            var plan = Build(a, b, c);

            Expect(Ids(plan.Ordered) == "C,B,A",
                "explicit chain is reversed into dependency order", failures);
            Expect(HasEdge(plan, "C", "B", PlanEdgeKind.Explicit) &&
                   HasEdge(plan, "B", "A", PlanEdgeKind.Explicit),
                "explicit chain creates explicit edges", failures);
        }

        private static void ExplicitDiamondTieBreak(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            var b = Make("B", Actions.no_action);
            var c = Make("C", Actions.no_action);
            var d = Make("D", Actions.no_action);

            b.dependsOn.Add("A");
            c.dependsOn.Add("A");
            d.dependsOn.Add("B");
            d.dependsOn.Add("C");

            var plan = Build(d, c, b, a);

            Expect(Ids(plan.Ordered) == "A,C,B,D",
                "diamond uses original index as tie-break", failures);
        }

        private static void CycleIsDeferred(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            var b = Make("B", Actions.no_action);

            a.dependsOn.Add("B");
            b.dependsOn.Add("A");

            var plan = Build(a, b);

            Expect(plan.Ordered.Count == 0,
                "cycle leaves no sortable instructions", failures);
            Expect(Ids(plan.Deferred) == "A,B",
                "cycle members are deferred in original order", failures);
            Expect(HasWarningContaining(plan, "cycle"),
                "cycle is reported as a diagnostic", failures);
        }

        private static void BlockedByCycleIsDeferred(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            var b = Make("B", Actions.no_action);
            var c = Make("C", Actions.no_action);

            a.dependsOn.Add("B");
            b.dependsOn.Add("A");
            c.dependsOn.Add("A");

            var plan = Build(c, a, b);

            Expect(plan.Ordered.Count == 0,
                "instruction blocked by a cycle is deferred", failures);
            Expect(Ids(plan.Deferred) == "C,A,B",
                "deferred instructions keep original order", failures);
        }

        private static void ImplicitCreateBeforeUse(List<string> failures)
        {
            var create = Make("1", Actions.create_object, ("Name", "Box"));
            var set = Make("2", Actions.set_property, ("Name", "Box"));

            var plan = Build(create, set);

            Expect(Ids(plan.Ordered) == "1,2",
                "create_object stays before its consumer", failures);
            Expect(HasEdge(plan, "1", "2", PlanEdgeKind.Implicit),
                "create_object -> set_property edge is implicit", failures);
        }

        private static void ImplicitDeclaredLater(List<string> failures)
        {
            var set = Make("1", Actions.set_property, ("Name", "Box"));
            var create = Make("2", Actions.create_object, ("Name", "Box"));

            var plan = Build(set, create);

            Expect(Ids(plan.Ordered) == "2,1",
                "a unique later definition is moved before its consumer", failures);
            Expect(HasEdge(plan, "2", "1", PlanEdgeKind.Implicit),
                "declared-later edge is implicit", failures);
        }

        private static void DeleteAfterUses(List<string> failures)
        {
            var create = Make("1", Actions.create_object, ("Name", "Box"));
            var set = Make("2", Actions.set_property, ("Name", "Box"));
            var delete = Make("3", Actions.delete_object, ("Name", "Box"));

            var plan = Build(create, set, delete);

            Expect(HasEdge(plan, "1", "2", PlanEdgeKind.Implicit) &&
                   HasEdge(plan, "1", "3", PlanEdgeKind.Implicit) &&
                   HasEdge(plan, "2", "3", PlanEdgeKind.Implicit),
                "delete_object follows definition and uses", failures);
            Expect(Ids(plan.Ordered) == "1,2,3",
                "delete_object keeps its position after uses", failures);
        }

        private static void SpaceOrder(List<string> failures)
        {
            var create = Make("1", Actions.create_space, ("SpaceName", "New"));
            var go = Make("2", Actions.goto_space, ("SpaceName", "New"));

            var plan = Build(create, go);

            Expect(HasEdge(plan, "1", "2", PlanEdgeKind.Implicit),
                "create_space -> goto_space edge is implicit", failures);
        }

        private static void SelectBarrier(List<string> failures)
        {
            var select = Make("1", Actions.select_object, ("Name", "Box"));
            var set = Make("2", Actions.set_property, ("Name", "Box"));

            var plan = Build(select, set);

            Expect(HasEdge(plan, "1", "2", PlanEdgeKind.Implicit),
                "select_object stays before later operations", failures);
        }

        private static void AttachReferences(List<string> failures)
        {
            var box = Make("1", Actions.create_object, ("Name", "Box"));
            var ball = Make("2", Actions.create_object, ("Name", "Ball"));
            var attach = Make("3", Actions.attach_to,
                ("Source", "Box"),
                ("Target", "Ball"));

            var plan = Build(box, ball, attach);

            Expect(HasEdge(plan, "1", "3", PlanEdgeKind.Implicit) &&
                   HasEdge(plan, "2", "3", PlanEdgeKind.Implicit),
                "attach_to depends on both Source and Target", failures);
        }

        private static void AttachToLaterDefinition(List<string> failures)
        {
            var camera = Make("1", Actions.create_object, ("Name", "Camera"));
            var attach = Make("2", Actions.attach_to,
                ("Source", "Camera"),
                ("Target", "Swat"));
            var swat = Make("3", Actions.create_object, ("Name", "Swat"));

            var plan = Build(camera, attach, swat);

            Expect(Ids(plan.Ordered) == "1,3,2",
                "attach_to follows a later create_object definition", failures);
            Expect(HasEdge(plan, "3", "2", PlanEdgeKind.Implicit),
                "declared-later edge is implicit", failures);
        }

        private static void DanglingExplicitDependency(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            a.dependsOn.Add("missing");

            var plan = Build(a);

            Expect(Ids(plan.Ordered) == "A",
                "dangling dependency does not block the instruction", failures);
            Expect(HasWarningContaining(plan, "unknown"),
                "dangling dependency is reported", failures);
        }

        private static void SelfDependency(List<string> failures)
        {
            var a = Make("A", Actions.no_action);
            a.dependsOn.Add("A");

            var plan = Build(a);

            Expect(Ids(plan.Ordered) == "A",
                "self dependency does not deadlock the planner", failures);
            Expect(plan.Edges.Count == 0,
                "self dependency produces no edge", failures);
        }

        private static void DuplicateIds(List<string> failures)
        {
            var plan = Build(
                Make("I1", Actions.no_action),
                Make("I1", Actions.no_action));

            Expect(plan.Ordered.Count == 2,
                "duplicate ids still produce a complete order", failures);
            Expect(plan.OriginalIndexById.Count == 2,
                "duplicate ids receive a synthetic key", failures);
            Expect(HasWarningContaining(plan, "Duplicate"),
                "duplicate id is reported", failures);
        }

        private static void NullInstruction(List<string> failures)
        {
            var plan = Build(Make("A", Actions.no_action), null);

            Expect(Ids(plan.Ordered) == "A",
                "null instruction is skipped", failures);
            Expect(HasWarningContaining(plan, "null"),
                "null instruction is reported", failures);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static Instruction Make(
            string id,
            string action,
            params (string Name, string Value)[] parameters)
        {
            var instruction = new Instruction
            {
                id = id,
                action = action,
                type = InstructionType.Elemental,
                state = InstructionState.Raw
            };

            foreach (var parameter in parameters)
            {
                instruction.parameters.Add(new Instruction.Parameter(
                    parameter.Name,
                    new JValue(parameter.Value)));
            }

            return instruction;
        }

        private static InstructionPlan Build(params Instruction[] instructions)
        {
            s_caseCount++;
            return InstructionPlanner.BuildPlan(instructions);
        }

        private static void Expect(
            bool condition,
            string description,
            List<string> failures)
        {
            if (!condition)
                failures.Add(description);
        }

        private static string Ids(List<Instruction> instructions)
        {
            var builder = new StringBuilder();

            foreach (Instruction instruction in instructions)
            {
                if (builder.Length > 0)
                    builder.Append(',');

                builder.Append(instruction.id);
            }

            return builder.ToString();
        }

        private static bool HasEdge(
            InstructionPlan plan,
            string from,
            string to,
            PlanEdgeKind kind)
        {
            foreach (PlanEdge edge in plan.Edges)
            {
                if (string.Equals(edge.From, from, StringComparison.Ordinal) &&
                    string.Equals(edge.To, to, StringComparison.Ordinal))
                {
                    return edge.Kind == kind;
                }
            }

            return false;
        }

        private static bool HasWarningContaining(
            InstructionPlan plan,
            string text)
        {
            foreach (PlanDiagnostic diagnostic in plan.Diagnostics)
            {
                if (diagnostic.Severity != PlanDiagnosticSeverity.Warning)
                    continue;

                if (diagnostic.Message.IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
