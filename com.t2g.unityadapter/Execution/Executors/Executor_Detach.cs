#if UNITY_EDITOR

using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace T2G
{
    [Executor(Actions.detach_from)]

    public class Executor_Detach : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("name");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }
            obj.transform.parent = null;
            Utils.UpdateEditorViews();
            await Task.Yield();

            string message = $"{objName} was dettached.";
            message += $"\nposition={obj.transform.localPosition};" +
                $"rotation={obj.transform.localRotation.eulerAngles};" +
                $"scale={obj.transform.localScale}";
            return (true, message, null);
        }
    }
}

#endif