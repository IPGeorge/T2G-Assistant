#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.remove_script)]
    public class Executor_RemoveScript : ExecutorBase
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

            Component componentObj = null;
            if (ComponentResolver.IsValidComponentName(ref componentType) ||
                FindComponentByName(obj, ref componentType))  //the ref parameter returns the validated type name if true
            {
                componentObj = obj.GetComponent(componentType);
                DestroyComponent(componentObj);
                await Task.Delay(100);
                return (true, $"{componentType} was removed from {objName}.", null);
            }
          
            return (false, $"Couldn't remove {componentType} from {objName}.", null);
        }

        public Component FindComponentByName(GameObject gameObject, ref string componentTypeName)
        {
            if (gameObject == null || string.IsNullOrWhiteSpace(componentTypeName))
            {
                return null;
            }

            // Get all components on the GameObject
            Component[] allComponents = gameObject.GetComponents<Component>();

            foreach (Component component in allComponents)
            {
                if (component == null) continue;

                // Check by type name
                string typeName = component.GetType().Name;
                if (string.Compare(typeName, componentTypeName, true) == 0)
                {
                    componentTypeName = typeName;
                    return component;
                }

                string fullName = component.GetType().FullName;
                if (string.Compare(fullName, componentTypeName, true) == 0)
                {
                    componentTypeName = fullName;
                    return component;
                }
            }
            return null;
        }


        void DestroyComponent(Component component)
        {
            if(component == null)
            {
                return;
            }
            GameObject.DestroyImmediate(component);
            Utils.UpdateEditorViews();
        }
    }
}

#endif
