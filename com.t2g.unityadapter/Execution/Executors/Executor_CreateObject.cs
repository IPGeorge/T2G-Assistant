#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEditor.Compilation;

namespace T2G
{
    [Executor(Actions.create_object)]
    public class Executor_CreateObject : ExecutorBase
    {
        static GameObject _newObj = null;

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string name = instruction.parameters.GetString("Name");
            string desc = instruction.desc;
            string positionStr = instruction.parameters.GetString("Position");
            Vector3? position = null;

            // Parse position if provided (format: "(x, y, z)")
            if (!string.IsNullOrEmpty(positionStr))
            {
                position = T2G.Utils.ParseVector3(positionStr);
            }

            _newObj = null;

            if (string.Compare(desc, "object", true) == 0)
            {
                _newObj = new GameObject(name);
            }
            else if(T2G.Utils.IsCameraDesc(desc))
            {
                _newObj = new GameObject(name);
                Camera camera = _newObj.AddComponent<Camera>();
                camera.orthographic = T2G.Utils.IsOrthographicsCameraDesc(desc);
                if (camera.orthographic)
                {
                    camera.orthographicSize = 5;
                }
                else
                {
                    camera.clearFlags = CameraClearFlags.Skybox;
                    camera.backgroundColor = Color.black;
                    camera.fieldOfView = 60f;
                }
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
                _newObj.AddComponent<AudioListener>();
            }
            else if(T2G.Utils.IsLightDesc(desc))
            {
                _newObj = new GameObject(name);
                Light light = _newObj.AddComponent<Light>();
                light.type = Utils.GetLightTypeFromDesc(desc);
                light.shadows = LightShadows.Soft;
                if (light.type == LightType.Directional)
                {
                    _newObj.transform.Rotate(45.0f, 45.0f, 0.0f);
                    light.color = new Color(0.957f, 0.914f, 0.608f, 1.0f);
                }
                else
                {
                    light.color = Color.white;
                }
                light.intensity = 1.0f;
                light.range = 10f;
            }
            else if (Utils.IsPrimitiveDesc(desc, out var primitiveType))
            {
                _newObj = GameObject.CreatePrimitive(primitiveType.Value);
                _newObj.name = name;
            }
            else if(desc.Contains("node object", System.StringComparison.OrdinalIgnoreCase))
            {
                _newObj = new GameObject(name);
            }
            
            if(_newObj == null)
            {
                await AssetImporter.ImportAssets(name, instruction.assets, position);
                await Utils.WaitForUnityIdle();

                // Try to create the object from the queue (may need to wait for package import)
                for (int retry = 0; retry < 60; retry++)
                {
                    AssetImporter.LoadLists();
                    if (AssetImporter.CreateObjectsList.Count > 0)
                    {
                        var objPrefab = AssetImporter.CreateObjectsList[0];
                        if (CreateObject(objPrefab))
                        {
                            ResultObjectId = _newObj?.GetComponent<T2GIdentifier>()?.Id ?? string.Empty;
                            AssetImporter.CreateObjectsList.RemoveAt(0);
                            AssetImporter.SaveLists();
                            return (true, $"{name} was created.", null);
                        }
                    }
                    await Task.Delay(1000);
                }
                return (false, $"Failed to create {name}!", null);
            }
            else
            {
                if (position.HasValue)
                {
                    _newObj.transform.position = position.Value;
                }
                else
                {
                Utils.PlaceInFrontOfCamera(_newObj);
            }
            var identifier = _newObj.AddComponent<T2GIdentifier>();
            identifier.Id = System.Guid.NewGuid().ToString();
            ResultObjectId = identifier.Id;
            Utils.UpdateEditorViews();
            return (true, $"{name} was created.", null);
            }
        }


        static bool CreateObject((string name, string targetRelPath, string position) objPrefab)
        {
            string prefabPath = Path.Combine("Assets", objPrefab.targetRelPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                _newObj = GameObject.Instantiate<GameObject>(prefabAsset);
                _newObj.name = objPrefab.name;
                var rectTransform = _newObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                   var canvas = Utils.CreateCanvasSystem();
                    rectTransform.parent = canvas.transform;
                    rectTransform.localPosition = Vector3.zero;
                }
                else if (string.IsNullOrEmpty(objPrefab.position))
                {
                    Utils.PlaceInFrontOfCamera(_newObj);
                }
                else
                {
                    Vector3? pos =  T2G.Utils.ParseVector3(objPrefab.position);
                    if (pos.HasValue)
                    {
                        _newObj.transform.position = pos.Value;
                    }
                    else
                    {
                        Utils.PlaceInFrontOfCamera(_newObj);
                    }
                }
                Utils.UpdateEditorViews();
                var idComp = _newObj.AddComponent<T2GIdentifier>();
                idComp.Id = System.Guid.NewGuid().ToString();
                return true;
            }
            _newObj = null;
            return false;
        }

        [InitializeOnLoadMethod]
        static async void CreateObjectImpl()
        {
            AssetImporter.LoadLists();

            while(AssetImporter.ImportAssetList.Count > 0 || !CommunicatorServer.Instance.ShakeHand)
            {
                await Task.Yield();
            }

            while (AssetImporter.CreateObjectsList.Count > 0)
            {
                Response response = new Response(); //Send delayed execution  response to the assistant
                var objPrefab = AssetImporter.CreateObjectsList[0];
                if (CreateObject(objPrefab))
                {
                    response.Succeeded = true;
                    response.Message = $"{objPrefab.name} was created.";
                    response.ObjectId = _newObj?.GetComponent<T2GIdentifier>()?.Id ?? string.Empty;
                }
                else
                {
                    response.Succeeded = false;
                    response.Message = $"Failed to create {objPrefab.name}!";
                }

                // Wait for Unity script compilation to finish
                if (EditorApplication.isCompiling)
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    void OnCompilationFinished(object obj)
                    {
                        CompilationPipeline.compilationFinished -= OnCompilationFinished;
                        tcs.TrySetResult(true);
                    }
                    CompilationPipeline.compilationFinished += OnCompilationFinished;
                    await tcs.Task;
                }

                // Wait for asset database to finish updating
                while (EditorApplication.isUpdating)
                {
                    await Task.Yield();
                }

                //Send response
                Execution.Instance.SendExecutionResponse(response);

                AssetImporter.CreateObjectsList.RemoveAt(0);
                AssetImporter.SaveLists();
            }
        }
    }
}

#endif