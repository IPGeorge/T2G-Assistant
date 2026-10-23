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
        public override async Task<(bool succeeded, 
            string message, 
            List<Response.Result> results,
            List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            // Never let a previous execution leak its ObjectId into this
            // response, especially when this execution fails.
            ResultObjectId = string.Empty;

            string name = instruction.parameters.GetString("Name");

            // create_object is a no-op when the object already exists in the
            // active space. Returning success keeps create_from replayable
            // without producing duplicate GameObjects.
            GameObject existing = string.IsNullOrWhiteSpace(name)
                ? null
                : Utils.FindObjectByName(name);

            if (existing != null)
            {
                ResultObjectId =
                    existing.GetComponent<T2GIdentifier>()?.Id ?? string.Empty;
                return (true, $"{name} already exists.", null, null);
            }

            string source;
            AssetType assetType = AssetType.Unknown;
            if (instruction.assets != null && instruction.assets.Count > 0)
            {
                source = instruction.assets[0].source;
                assetType = instruction.assets[0].type;
            }
            else
            {
                source = string.Empty;
            }

            // GameDesc-generated create_object instructions carry the semantic
            // description in "desc" rather than in an asset source. A node
            // object has no asset at all, so check both places.
            string desc = instruction.desc ?? string.Empty;

            string positionStr = instruction.parameters.GetString("Position");
            Vector3? position = null;

            // Parse position if provided (format: "(x, y, z)")
            if (!string.IsNullOrEmpty(positionStr))
            {
                position = T2G.Utils.ParseVector3(positionStr);
            }

            _newObj = null;

            if (assetType == AssetType.Node)  //Add node object
            {
                _newObj = new GameObject(name);
            }
            else if (T2G.Utils.IsCameraDesc(source))  //Add camera
            {
                _newObj = new GameObject(name);
                Camera camera = _newObj.AddComponent<Camera>();
                camera.orthographic = T2G.Utils.IsOrthographicsCameraDesc(source);
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
            else if (T2G.Utils.IsLightDesc(source))   //add light
            {
                _newObj = new GameObject(name);
                Light light = _newObj.AddComponent<Light>();
                light.type = Utils.GetLightTypeFromDesc(source);
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
            else if (Utils.IsPrimitiveDesc(source, out var primitiveType))        //add primitive
            {
                _newObj = GameObject.CreatePrimitive(primitiveType.Value);
                _newObj.name = name;
            }
            else if (source.Contains("node object", System.StringComparison.OrdinalIgnoreCase) ||
                     desc.Contains("node object", System.StringComparison.OrdinalIgnoreCase)) //new node
            {
                _newObj = new GameObject(name);
            }

            if (_newObj == null)
            {
                await AssetImporter.ImportAssets(name, instruction.assets, position, instruction.id);
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
                            return (true, $"{name} was created.", null, null);
                        }
                    }
                    await Task.Delay(1000);
                }
                return (false, $"Failed to create {name}!", null, null);
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
                return (true, $"{name} was created.", null, null);
            }
        }


        static bool CreateObject((string name, string targetRelPath, string position, string instructionId) objPrefab)
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
                    Vector3? pos = T2G.Utils.ParseVector3(objPrefab.position);
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

            while (AssetImporter.ImportAssetList.Count > 0 || !CommunicatorServer.Instance.ShakeHand)
            {
                await Task.Yield();
            }

            while (AssetImporter.CreateObjectsList.Count > 0)
            {
                var objPrefab = AssetImporter.CreateObjectsList[0];

                // Send delayed execution response to the assistant. The id is
                // restored from the persisted list so the response is matched
                // to the create_object instruction that started this work.
                Response response = new Response
                {
                    InstructionId = objPrefab.instructionId ?? string.Empty
                };
                if (CreateObject(objPrefab))
                {
                    response.Succeeded = true;
                    response.Message = $"{objPrefab.name} was created.";
                    string objectId =
                        _newObj?.GetComponent<T2GIdentifier>()?.Id ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(objectId))
                    {
                        response.AddResult(
                            "ObjectId",
                            "String",
                            objectId);
                    }

                    // Return the actual transform established by Unity.
                    if (_newObj != null)
                    {
                        Vector3 position = _newObj.transform.position;
                        response.AddResult(
                            "Position",
                            "Vector3",
                            new float[] { position.x, position.y, position.z });
                    }
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