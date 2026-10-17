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

            List<Instruction.Asset> scriptAssets = new List<Instruction.Asset>();

            for (int i = 0; i < instruction.assets.Count; ++i)
            {
                if (instruction.assets[i] == null)
                {
                    continue;
                }

                if (i > 0 || instruction.assets[i].type == AssetType.SourceAsset)
                    //If adding component or local script, only one is suppoerted at this time
                {
                    scriptAssets.Add(instruction.assets[i]);
                    continue;
                }

                if (i == 0 && instruction.assets[0].type == AssetType.Script)
                {
                    string script = instruction.assets[0].source;
                    string componentTypeName = T2G.Utils.GetMonoBehaviourClassName(script);
                    string target = Path.Combine(Application.dataPath, "Scripts", componentTypeName + ".cs");
                    string targetDir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // Check if the file already exists and has the exact same content
                    if (File.Exists(target) && File.ReadAllText(target) == script)
                    {
                        var result = AddScriptComponent(obj, componentTypeName);
                        if (result.succeeded)
                        {
                            Utils.UpdateEditorViews();
                            resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = componentTypeName });
                        }
                        return (result.succeeded, result.responseMessage, resultList, null);
                    }

                    EditorPrefs.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{componentTypeName}");
                    File.WriteAllText(target, script);
                    AssetDatabase.Refresh();
                    CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
                    return (true, "This line should be ignored by the execution", resultList, null);    //This line won't be reached
                }

                if (instruction.assets[i].type == AssetType.Identifier)
                {
                    var result = AddScriptComponent(obj, instruction.assets[i].source);
                    if (result.succeeded)
                    {
                        Utils.UpdateEditorViews();
                        resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = instruction.assets[i].source });
                        return (true, result.responseMessage, resultList, null);
                    }
                    else
                    {
                        return (false, result.responseMessage, null, null);
                    }
                }
                else if (instruction.assets[i].type == AssetType.ScriptFile)
                {
                    string dest;
                    string source = instruction.assets[i].source;
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

                    if (File.Exists(dest) && File.ReadAllText(dest).Trim() == script.Trim())
                    {
                        //Directly add the component in case it is available
                        var result = AddScriptComponent(obj, componentTypeName);
                        if (result.succeeded)
                        {
                            Utils.UpdateEditorViews();
                            resultList.Add(new Response.Result() { name = "ComponentType", type = "string", value = componentTypeName });
                        }
                        return (result.succeeded, result.responseMessage, resultList, null);
                    }

                    string directory = Path.GetDirectoryName(dest);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    EditorPrefs.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{componentTypeName}");
                    File.Copy(source, dest, true);

                    AssetDatabase.Refresh();
                    CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);

                    return (true, "This line should be ignored by the execution", resultList, null);    //This line won't be reached
                }
            }

            if (scriptAssets.Count > 0)
            {
                string script = scriptAssets[0].source;
                string componentTypeName = string.Empty;
                var importResult = await AssetImporter.BeginImportScripts(scriptAssets);
                if (!importResult.succeeded)
                {
                    return (false, $"One or some scripts were not imported!", null, null);
                }
                componentTypeName = importResult.componentTypeName;
                EditorPrefs.SetString(k_InitOnLoadAddComponentKey, $"{instruction.id},{objName},{componentTypeName}");

                await AssetImporter.EndImportScripts(componentType);
                
                return (true, "This line should be ignored by the execution", resultList, null);    //This line won't be reached
            }

            return (false, "No component needs to be added.", null, null);
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
            List<Response.Result> resultList = new List<Response.Result>();

            if (!EditorPrefs.HasKey(k_InitOnLoadAddComponentKey))
            {
                return;
            }

            while (!CommunicatorServer.Instance.IsConnected || !CommunicatorServer.Instance.ShakeHand)
            {
                await Task.Yield();
            }

            Response response = new Response();
            response.Results = resultList;
            string keyValue = EditorPrefs.GetString(k_InitOnLoadAddComponentKey);
            string[] keyPair = keyValue.Split(',');
            EditorPrefs.DeleteKey(k_InitOnLoadAddComponentKey);
            if (keyPair.Length != 3)
            {
                response.Succeeded = false;
                response.Message = $"Failed to add component.";
                Execution.Instance.SendExecutionResponse(response);
                return;
            }
            response.InstructionId = keyPair[0];
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
            var result = AddScriptComponent(obj, componentTypeName);
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
