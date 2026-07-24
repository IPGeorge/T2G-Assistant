#if UNITY_EDITOR

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G
{
    [Executor(Actions.set_relationship)]
    public class Executor_SetRelationship : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string sourceName = instruction.parameters.GetString("source");
            string relType = instruction.parameters.GetString("type");
            string targetName = instruction.parameters.GetString("target");
            string slotName = instruction.parameters.GetString("slot");

            if (string.IsNullOrWhiteSpace(sourceName))
                return (false, "source is required.", null);

            var sourceObject = Utils.FindObjectByName(sourceName);
            if (sourceObject == null)
                return (false, $"Couldn't find source object '{sourceName}'.", null);

            // For hierarchy-affecting relationship types, do transform parenting
            if (string.Equals(relType, Actions.attach_to, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relType, "contains", System.StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(targetName))
                    return (false, "target is required for attach/contain relationships.", null);

                var targetObject = Utils.FindObjectByName(targetName);
                if (targetObject == null)
                    return (false, $"Couldn't find target object '{targetName}'.", null);

                Transform targetTransform = targetObject.transform;
                if (!string.IsNullOrWhiteSpace(slotName))
                {
                    var socketTransform = targetObject.transform.Find(slotName);
                    if (socketTransform != null)
                    {
                        targetTransform = socketTransform;
                    }
                    else
                    {
                        socketTransform = FindChildByName(targetObject.transform, slotName);
                        if (socketTransform != null)
                        {
                            targetTransform = socketTransform;
                        }
                        else
                        {
                            Debug.LogWarning($"[Executor_SetRelationship] Socket '{slotName}' not found on {targetName}, attaching to object instead.");
                        }
                    }

                    sourceObject.transform.SetParent(targetTransform, false);
                    sourceObject.transform.localPosition = Vector3.zero;
                    sourceObject.transform.localRotation = Quaternion.identity;
                    sourceObject.transform.localScale = Vector3.one;
                }
                else
                {
                    sourceObject.transform.SetParent(targetTransform, true);
                }
            }
            // All other relationship types are semantic/metadata only
            // Data model update is handled by GameDescManager.UpdateFromInstruction

            Utils.UpdateEditorViews();
            await Task.Delay(100);

            string message = string.IsNullOrWhiteSpace(targetName)
                ? $"Relationship '{relType}' set on {sourceName}."
                : $"Relationship '{relType}' from {sourceName} to {targetName} set.";

            return (true, message, null);
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

#endif
