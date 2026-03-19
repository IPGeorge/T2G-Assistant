#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.remove_component)]
    public class Executor_RemoveComponent : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objName");
            string componentType = instruction.parameters.GetString("componentType");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }

            var componentObj = obj.GetComponent(componentType);
            if(componentObj != null)
            {
                GameObject.Destroy(componentObj);
                await Task.Delay(100);
                Utils.UpdateEditorViews();
                return (true, $"{componentType} was removed from {objName}.", null);
            }
           
            return (false, $"Couldn't remove {componentType} from {objName}.", null);
        }
    }
}

#endif
