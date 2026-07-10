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
            string sourceObjName = instruction.parameters.GetString("Source");
            string targetObjName = instruction.parameters.GetString("Target");
            string socketName = instruction.parameters.GetString("Socket");

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

            Transform targetTransform = targetObject.transform;
            bool isSocketTarget = !string.IsNullOrEmpty(socketName);

            if (isSocketTarget)
            {
                // Search for bone/socket in target object's hierarchy
                var socketTransform = targetObject.transform.Find(socketName);
                if (socketTransform != null)
                {
                    targetTransform = socketTransform;
                }
                else
                {
                    // Also search recursively in children
                    socketTransform = FindChildByName(targetObject.transform, socketName);
                    if (socketTransform != null)
                    {
                        targetTransform = socketTransform;
                    }
                    else
                    {
                        Debug.LogWarning($"[Executor_Attach] Bone/socket '{socketName}' not found on {targetObjName}, attaching to object instead.");
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

            Utils.UpdateEditorViews();
            await Task.Delay(100);
            
            string message = isSocketTarget 
                ? $"{sourceObjName} was attached to {targetObjName} at {socketName}."
                : $"{sourceObjName} was attached to {targetObjName}.";

            message += $"\nlocalPosition={sourceObject.transform.localPosition};" + 
                $"localRotation={sourceObject.transform.localRotation.eulerAngles};" + 
                $"localScale={sourceObject.transform.localScale}";

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