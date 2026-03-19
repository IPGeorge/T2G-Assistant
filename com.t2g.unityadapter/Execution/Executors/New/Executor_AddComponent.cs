#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.add_component)]
    public class Executor_Addcomponent : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objName");
            string component = instruction.parameters.GetString("component");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }

            if(string.Compare(instruction.desc, "file", true) == 0)
            {
                if(!File.Exists(component))
                {
                    return (false, $"Couldn't find the source file {component}.", null);
                }

                string dest = Path.Combine(Application.dataPath, "Scripts", Path.GetFileName(component));
                if(File.Exists(dest))
                {

                }
                else
                {
                    File.Copy(component, dest, true);
                }
            }
            else if (string.Compare(instruction.desc, "component", true) == 0)
            {
                var addedComponent = obj.AddComponentByName(component);
                if(addedComponent == null)
                {
                    return (false, $"Failed to add {component} to {objName}.", null);
                }
                else
                {
                    return (false, $"{component} was added to {objName}.", null);
                }
            }
            else
            {
                
            }

            Utils.UpdateEditorViews();

            await Task.Delay(100);
            return (true, $"{component} was added to {objName}.", null);
        }
    }


}

#endif
