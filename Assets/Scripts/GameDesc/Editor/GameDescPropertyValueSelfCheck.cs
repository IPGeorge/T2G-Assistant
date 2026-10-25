using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using T2G;
using UnityEditor;
using UnityEngine;

namespace T2G.Assistant
{
    /// <summary>
    /// Lightweight self-check for the GameDesc property value normalization.
    ///
    /// Guards the round-trip that previously produced a "String" Position
    /// holding a JSON array:
    ///   execution response (array) -> GameDesc (scalar) -> replayed instruction
    /// It also verifies that older/hand-edited GameDesc files still yield
    /// scalar instruction parameters.
    ///
    /// The types under test live in Assembly-CSharp (no asmdef), which cannot
    /// be referenced by an EditMode test assembly, so this editor-only runner
    /// keeps the checks available without adding an assembly definition.
    /// </summary>
    public static class GameDescPropertyValueSelfCheck
    {
        private static int s_caseCount;

        [MenuItem("Tools/T2G/GameDesc/Run Self-Check")]
        public static void Run()
        {
            var failures = new List<string>();
            s_caseCount = 0;

            CoerceStringArrayToVectorText(failures);
            CoerceLeavesTypedContainer(failures);
            CoerceLeavesStringValue(failures);
            CoerceLeavesNull(failures);
            InstructionValueFlattensContainers(failures);
            ReplayCreateObjectPositionIsScalar(failures);
            ReplaySetPropertyValueIsScalar(failures);
            ReplayWithoutPositionOmitsParameter(failures);

            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[GameDescPropertyValue] Self-check passed ({s_caseCount} cases).");
                return;
            }

            Debug.LogError(
                $"[GameDescPropertyValue] Self-check failed " +
                $"({failures.Count}/{s_caseCount} cases):\n - " +
                string.Join("\n - ", failures));
        }

        // -----------------------------------------------------------------
        // Cases
        // -----------------------------------------------------------------

        private static void CoerceStringArrayToVectorText(List<string> failures)
        {
            s_caseCount++;

            JToken stored = GameDescPropertyValue.Coerce(
                "String",
                new JArray(276.0, 155.25, 0.0));

            Expect(stored.Type == JTokenType.String,
                "array stored under String type becomes a string", failures);
            Expect(stored.Value<string>() == "(276,155.25,0)",
                "numeric array stored as (x,y,z)", failures);
        }

        private static void CoerceLeavesTypedContainer(List<string> failures)
        {
            s_caseCount++;

            JToken value = new JArray(1, 2, 3);
            JToken stored = GameDescPropertyValue.Coerce("Vector3", value);

            Expect(ReferenceEquals(stored, value),
                "non-string type keeps its container value", failures);
        }

        private static void CoerceLeavesStringValue(List<string> failures)
        {
            s_caseCount++;

            JToken stored = GameDescPropertyValue.Coerce(
                "String",
                new JValue("(1,2,3)"));

            Expect(stored.Type == JTokenType.String &&
                   stored.Value<string>() == "(1,2,3)",
                "string value is unchanged", failures);
        }

        private static void CoerceLeavesNull(List<string> failures)
        {
            s_caseCount++;

            JToken stored = GameDescPropertyValue.Coerce(
                "String",
                JValue.CreateNull());

            Expect(stored != null && stored.Type == JTokenType.Null,
                "null value is unchanged", failures);
        }

        private static void InstructionValueFlattensContainers(List<string> failures)
        {
            s_caseCount++;

            JToken vector = GameDescPropertyValue.ToInstructionValue(
                new JArray(4.5, 5.5, 6.5));

            Expect(vector.Type == JTokenType.String &&
                   vector.Value<string>() == "(4.5,5.5,6.5)",
                "numeric array becomes vector text", failures);

            JToken container = GameDescPropertyValue.ToInstructionValue(
                new JObject { ["a"] = 1 });

            Expect(container.Type == JTokenType.String &&
                   container.Value<string>() == "{\"a\":1}",
                "object becomes compact JSON text", failures);
        }

        private static void ReplayCreateObjectPositionIsScalar(List<string> failures)
        {
            s_caseCount++;

            Object obj = MakeObject(new PropertyDesc
            {
                Name = "Position",
                Type = "String",
                Value = new JArray(276.0, 155.25, 0.0)
            });

            Instruction create = Find(Parse(obj), Actions.create_object);
            Expect(create != null,
                "create_object is emitted", failures);

            Instruction.Parameter position =
                create?.parameters.Find(p => p.name == "Position");

            Expect(position != null && position.value.Type == JTokenType.String,
                "replayed Position value is a scalar string", failures);
            Expect(position != null && position.type == "String",
                "replayed Position keeps its declared type", failures);

            bool parsed = position != null &&
                          position.value is JValue scalar &&
                          scalar.Type == JTokenType.String &&
                          T2G.Utils.ParseVector3(scalar.Value<string>()).HasValue;
            Expect(parsed,
                "replayed Position string is parseable as a Vector3", failures);
        }

        private static void ReplaySetPropertyValueIsScalar(List<string> failures)
        {
            s_caseCount++;

            Object obj = MakeObject(new PropertyDesc
            {
                Name = "localScale",
                Type = "Vector3",
                Value = new JArray(1, 2, 3)
            });

            Instruction set = Find(Parse(obj), Actions.set_property);
            Instruction.Parameter value =
                set?.parameters.Find(p => p.name == "Value");

            Expect(value != null &&
                   value.value.Type == JTokenType.String &&
                   value.value.Value<string>() == "(1,2,3)",
                "replayed set_property value is a scalar vector string", failures);
        }

        private static void ReplayWithoutPositionOmitsParameter(List<string> failures)
        {
            s_caseCount++;

            Object obj = MakeObject(new PropertyDesc
            {
                Name = "Color",
                Type = "String",
                Value = new JValue("red")
            });

            Instruction create = Find(Parse(obj), Actions.create_object);

            Expect(create != null &&
                   !create.parameters.Exists(p => p.name == "Position"),
                "create_object without a Position property emits no Position parameter", failures);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static Object MakeObject(params PropertyDesc[] properties)
        {
            var obj = new Object
            {
                Id = Guid.NewGuid().ToString(),
                Name = "GameUI",
                Desc = "shooter game UI"
            };

            if (properties != null)
                obj.Properties.AddRange(properties);

            return obj;
        }

        private static Instruction[] Parse(Object obj)
        {
            var space = new T2G.Assistant.Space
            {
                Id = Guid.NewGuid().ToString(),
                Name = "ShootingRange"
            };

            return GameDescParser.ParseObjectForInstructions(obj, space);
        }

        private static Instruction Find(Instruction[] instructions, string action)
        {
            return instructions == null
                ? null
                : Array.Find(instructions, i => i != null && i.action == action);
        }

        private static void Expect(
            bool condition,
            string description,
            List<string> failures)
        {
            if (!condition)
                failures.Add(description);
        }
    }
}
