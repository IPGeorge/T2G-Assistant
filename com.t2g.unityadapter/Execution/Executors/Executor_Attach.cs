#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.attach_to)]
    public class Executor_Attach : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string sourceObjName = instruction.parameters.GetString("source");
            string targetObjName = instruction.parameters.GetString("target");
            string boneName = instruction.parameters.GetString("bone");

            var sourceObject = Utils.FindObjectByName(sourceObjName);
            if(sourceObject == null)
            {
                return (false, $"Couldn't find {sourceObjName}!", null);
            }
            var targetObject = Utils.FindObjectByName(targetObjName);
            if (targetObject == null)
            {
                return (false, $"Couldn't find {targetObjName}!", null);
            }

            // If bone is specified, try to find the bone/socket transform
            Transform attachTarget = targetObject.transform;
            if (!string.IsNullOrEmpty(boneName))
            {
                // Search for bone/socket in target object's hierarchy
                var boneTransform = targetObject.transform.Find(boneName);
                if (boneTransform != null)
                {
                    attachTarget = boneTransform;
                }
                else
                {
                    // Also search recursively in children
                    boneTransform = FindChildByName(targetObject.transform, boneName);
                    if (boneTransform != null)
                    {
                        attachTarget = boneTransform;
                    }
                    else
                    {
                        Debug.LogWarning($"[Executor_Attach] Bone/socket '{boneName}' not found on {targetObjName}, attaching to object instead.");
                    }
                }
            }

            sourceObject.transform.SetParent(attachTarget, false);
            sourceObject.transform.localPosition = Vector3.zero;
            sourceObject.transform.localRotation = Quaternion.identity;
            sourceObject.transform.localScale = Vector3.one;

            Utils.UpdateEditorViews();
            await Task.Delay(100);
            T2G.Utils.UpdateEditorViews();
            
            string message = string.IsNullOrEmpty(boneName) 
                ? $"{sourceObjName} was attached to {targetObjName}."
                : $"{sourceObjName} was attached to {targetObjName} at {boneName}.";
            
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