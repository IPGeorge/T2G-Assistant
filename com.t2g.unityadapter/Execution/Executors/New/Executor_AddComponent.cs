#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.add_component)]
    public class Executor_Addcomponent : ExecutorBase
    {
        static readonly string k_InitOnLoadAddComponentKey = "AddComponentTask";

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objName");
            string component = instruction.parameters.GetString("component");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }

            if (string.Compare(instruction.desc, "component", true) == 0)
            {
                var result = AddScriptComponent(obj, component);
                return (result.succeeded, result.responseMessage, null);
            }
            else 
            {
                string source, dest;


                if (string.Compare(instruction.desc, "file", true) == 0)
                {
                    source = component;
                    
                }
                else
                {
                    if (instruction.assets != null && instruction.assets.Count > 0)
                    {
                        source = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, instruction.assets[0]);
                    }
                    else
                    {
                        return (false, $"No resolved asset for {component}.", null);
                    }
                }

                if (!File.Exists(component))
                {
                    return (false, $"Couldn't find the source file {source}.", null);
                }

                dest = Path.Combine(Application.dataPath, "Scripts", Path.GetFileName(source));
                string script = File.ReadAllText(source);
                string componentTypeName = T2G.Utils.GetMonoBehaviourClassName(script);
                EditorPrefs.SetString(k_InitOnLoadAddComponentKey, objName + "," + componentTypeName);
                File.Copy(component, dest, true);
                if (componentTypeName != null)
                {
                    var result = AddScriptComponent(obj, componentTypeName);
                    Utils.UpdateEditorViews();
                    await Task.Delay(100);
                    EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
                    return (result.succeeded, result.responseMessage, null);
                }
                else
                {
                    EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
                    return (false, "Invalid component file .", null);
                }
            }
        }

        static (bool succeeded, string responseMessage) AddScriptComponent(GameObject obj, string componentTypeName)
        {
            if (obj.GetComponent(componentTypeName) == null)
            {
                var addedComponent = obj.AddComponentByName(componentTypeName);
                if (addedComponent == null)
                {
                    return (false, $"Failed to add {componentTypeName} to {obj.name}.");
                }
            }
            return (false, $"{componentTypeName} was added to {obj.name}.");
        }


        [InitializeOnLoadMethod]
        public static void AddComponentAfterInitOnLoad()
        {
            if(!EditorPrefs.HasKey(k_InitOnLoadAddComponentKey))
            {
                return;
            }

            Response response = new Response();
            string keyValue = EditorPrefs.GetString(k_InitOnLoadAddComponentKey);
            string[] keyPair = keyValue.Split(',');
            EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
            if (keyPair.Length != 2)
            {
                response.Succeeded = false;
                response.Message = $"Failed to add component.";
                Execution.Instance.SendExecutionResponse(response);
                return;
            }
            string objName = keyPair[0];
            string componentTypeName = keyPair[1];
            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                response.Succeeded = false;
                response.Message = $"Coudn't find {objName}.";
                Execution.Instance.SendExecutionResponse(response);
                return;
            }
            var result = AddScriptComponent(obj, componentTypeName);
            Utils.UpdateEditorViews();
            EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
        }
    }

}

#endif
