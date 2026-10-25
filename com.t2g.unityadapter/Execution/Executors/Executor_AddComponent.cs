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
    public class Executor_AddComponent : ExecutorBase
    {
        static readonly string k_InitOnLoadAddComponentKey = "AddComponentTask";

        public override async Task<(bool succeeded, 
            string message, 
            List<Response.Result> results, 
            List<Instruction> additionalInstructions)>  Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("Name");
            string componentType = instruction.parameters.GetString("Type");
            List<Response.Result> resultList = new List<Response.Result>();

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null, null);
            }

            // Check if this component is available
            if (Utils.IsValidComponentName(componentType)) 
            {
                var result = AddScriptComponent(obj, componentType, "Validator");
                if (result.succeeded)
                {
                    Utils.UpdateEditorViews();
                    resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = componentType });
                    return (true, result.responseMessage, resultList, null);
                }
                return (false, result.responseMessage, null, null);
            }

            if (instruction.assets == null || instruction.assets.Count <= 0)
            {
                return (false, $"No component needs to be attached to {objName}!", null, null);
            }

            var asset = instruction.assets[0]; //If adding component or local script, only one is suppoerted at this time

            if (asset.type == AssetType.Script)
            {
                string script = asset.source;
                string componentTypeName = T2G.Utils.GetMonoBehaviourClassName(script);
                string target = Path.Combine(Application.dataPath, "Scripts", componentTypeName + ".cs");
                string targetDir = Path.GetDirectoryName(target);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                SessionState.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{componentTypeName}");
                File.WriteAllText(target, script);
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);

                resultList.Clear(); //ensure it is empty, so the execution result message is defered after the reload.
                return (true, $"Wait for reload to add {componentType} to {objName}. ", resultList, null);
            }
            else  if (asset.type == AssetType.Identifier)
            {
                var result = AddScriptComponent(obj, asset.source, "Idenfifier");
                if (result.succeeded)
                {
                    Utils.UpdateEditorViews();
                    resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = asset.source });
                    return (true, result.responseMessage, resultList, null);
                }
                return (false, result.responseMessage, null, null);
            }
            else if (asset.type == AssetType.ScriptFile)
            {
                string dest;
                string source = asset.source;
                dest = Path.Combine(Application.dataPath, "Scripts", Path.GetFileName(source));
                string script = File.ReadAllText(source);
                string componentTypeName = T2G.Utils.GetMonoBehaviourClassName(script);
                if (string.IsNullOrEmpty(componentTypeName))
                {
                    return (false, $"Invalid component type name.", null, null);
                }


                if (!File.Exists(source))
                {
                    return (false, $"Couldn't find the source file {source}.", null, null);
                }

                string directory = Path.GetDirectoryName(dest);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SessionState.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{componentTypeName}");
                File.Copy(source, dest, true);

                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);

                resultList.Clear(); //ensure it is empty, so the execution result message is defered after the reload.
                return (true, $"Wait for reload to add {componentType} to {objName}", resultList, null);
            }
            else if(asset.type == AssetType.SourceAsset)
            {
                List<string> scriptAssets = new List<string>(asset.source.Split(","));

                var result = await AssetImporter.BeginImportScripts(instruction.assets);
                if(result.succeeded)
                {
                    SessionState.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{result.componentTypeName}");

                    await AssetImporter.EndImportScripts(result.componentTypeName);

                    resultList.Clear(); //ensure it is empty, so the execution result message is defered after the reload.
                    return (true, $"Wait for reload to add {componentType} to {objName}", resultList, null);
                }
            }

            return (false, $"Failed to add invalid asset {componentType} as a component to {objName}", null, null);
        }

        static (bool succeeded, string responseMessage) AddScriptComponent(GameObject obj, string componentTypeName, string caller = null)
        {
            bool result = true;
            string message = string.Empty;
            
            if (obj.GetComponent(componentTypeName) == null)
            {
                var addedComponent = obj.AddComponentByName(componentTypeName);
                if (addedComponent == null)
                {
                    result = false;
                    message = $"Failed to add {componentTypeName} to {obj.name}.";
                }
                else
                {
                    message = $"{componentTypeName} was added to {obj.name}.";
                }
            }
            else
            {
                message = $"{componentTypeName} was already added to {obj.name}.";
            }

            if(caller != null)
            {
                message = $"[{caller }] {message}";
            }

            return (result, message);
        }


        [InitializeOnLoadMethod]
        public static async void AddComponentAfterInitOnLoad()
        {
            List<Response.Result> resultList = new List<Response.Result>();

            string keyValue = SessionState.GetString(k_InitOnLoadAddComponentKey, string.Empty);
            if (string.IsNullOrEmpty(keyValue))
            {
                return;
            }

            while (!CommunicatorServer.Instance.IsConnected || !CommunicatorServer.Instance.ShakeHand)
            {
                await Task.Yield();
            }

            Response response = new Response();
            response.Results = resultList;
            string[] keyPair = keyValue.Split(',');
            SessionState.EraseString(k_InitOnLoadAddComponentKey);
            response.InstructionId = keyPair.Length > 0 ? keyPair[0] : string.Empty;
            if (keyPair.Length != 3)
            {
                response.Succeeded = false;
                response.Message = $"Failed to add component.";
                Execution.Instance.SendExecutionResponse(response);
                return;
            }
            string objName = keyPair[1];
            string componentTypeName = keyPair[2];
            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                response.Succeeded = false;
                response.Message = $"Coudn't find {objName}.";
                Execution.Instance.SendExecutionResponse(response);
                return;
            }
            var result = AddScriptComponent(obj, componentTypeName, "Reload");
            response.Succeeded = result.succeeded;
            response.Message = result.responseMessage;
            if (response.Succeeded)
            {
                resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = componentTypeName });
                Utils.UpdateEditorViews();
            }
            Execution.Instance.SendExecutionResponse(response);
        }
    }

}

#endif
