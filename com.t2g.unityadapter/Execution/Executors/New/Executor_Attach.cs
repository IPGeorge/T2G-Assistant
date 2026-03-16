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

            var sourceObject = Utils.FindObjectByName(sourceObjName);
            if(sourceObject == null)
            {
                return (false, $"Couldn't find {sourceObjName}!", null);
            }
            var targetObject = Utils.FindObjectByName(targetObjName);
            if (targetObject == null)
            {
                return (false, $"Couldn't find {targetObject}!", null);
            }

            sourceObject.transform.parent = targetObject.transform;

            Utils.UpdateEditorViews();
            await Task.Delay(100);
            return (true, $"{sourceObjName} was attached to {targetObjName}.", null);
        }
    }
}

#endif