#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
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
            string componentType = instruction.parameters.GetString("type");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }

            if (string.Compare(componentType, "component", true) == 0)
            {
                var result = AddScriptComponent(obj, instruction.desc);
                if (result.succeeded)
                {
                    Utils.UpdateEditorViews();
                    return (true, result.responseMessage, null);
                }
                else
                {
                    return (false, result.responseMessage, null);
                }
            }
            else 
            {
                string source, dest;

                if (string.Compare(componentType, "file", true) == 0)
                {
                    source = instruction.desc;
                }
                else
                {
                    if (instruction.assets != null && instruction.assets.Count > 0)
                    {
                        source = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, instruction.assets[0]);
                    }
                    else
                    {
                        return (false, $"No resolved asset for {instruction.desc}.", null);
                    }
                }

                if (!File.Exists(source))
                {
                    return (false, $"Couldn't find the source file {source}.", null);
                }

                var tcs = new TaskCompletionSource<bool>();
                dest = Path.Combine(Application.dataPath, "Scripts", Path.GetFileName(source));
                string script = File.ReadAllText(source);
                string componentTypeName = T2G.Utils.GetMonoBehaviourClassName(script);
                if(string.IsNullOrEmpty(componentTypeName))
                {
                    return (false, $"Invalid component type name.", null);
                }

                //Directly add the component in case it is available
                var result = AddScriptComponent(obj, componentTypeName);
                if (result.succeeded)
                {
                    Utils.UpdateEditorViews();
                    return (true, result.responseMessage, null);
                }

                EditorPrefs.SetString(k_InitOnLoadAddComponentKey, objName + "," + componentTypeName);
                string directory = Path.GetDirectoryName(dest);
                if(!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(source, dest, true);
                AssetDatabase.Refresh();

                void OnCompilationFinished(object param)
                {
                    CompilationPipeline.compilationFinished -= OnCompilationFinished;
                    tcs.SetResult(true);
                }

                CompilationPipeline.compilationFinished += OnCompilationFinished;
                bool completed = await tcs.Task;
                if (componentTypeName != null)
                {
                    result = AddScriptComponent(obj, componentTypeName);
                    if (result.succeeded)
                    {
                        EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
                        Utils.UpdateEditorViews();
                    }
                    return (result.succeeded, result.responseMessage, null);
                }
                else
                {
                    EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
                    return (false, "Invalid component file.", null);
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
                else
                {
                    return (true, $"{componentTypeName} was added to {obj.name}.");
                }
            }
            else
            {
                return (true, $"{componentTypeName} was already added to {obj.name}.");
            }
        }


        [InitializeOnLoadMethod]
        public static async void AddComponentAfterInitOnLoad()
        {
            if(!EditorPrefs.HasKey(k_InitOnLoadAddComponentKey))
            {
                return;
            }

            while (!CommunicatorServer.Instance.IsConnected || !Execution.Instance.ShakeHand)
            {
                await Task.Yield();
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
            response.Succeeded = result.succeeded;
            response.Message = result.responseMessage;
            Execution.Instance.SendExecutionResponse(response);
            Utils.UpdateEditorViews();
        }
    }

}

#endif
