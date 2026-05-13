#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

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
            string positionStr = instruction.parameters.GetString("position");
            Vector3? position = null;

            // Parse position if provided (format: "(x, y, z)")
            if (!string.IsNullOrEmpty(positionStr))
            {
                position = T2G.Utils.ParsePosition(positionStr);
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
            
            if(_newObj == null)
            {
                await AssetImporter.ImportAssets(name, instruction.assets, position);
                CreateObjectImpl();
                return (false, null, null);
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
                if(string.IsNullOrEmpty(objPrefab.position))
                {
                    Utils.PlaceInFrontOfCamera(_newObj);
                }
                else
                {
                    Vector3? pos =  T2G.Utils.ParsePosition(objPrefab.position);
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
                return true;
            }
            _newObj = null;
            return false;
        }

        [InitializeOnLoadMethod]
        static async void CreateObjectImpl()
        {
            AssetImporter.LoadLists();

            while(AssetImporter.ImportAssetList.Count > 0 || !Execution.Instance.ShakeHand)
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
                    Execution.Instance.SendExecutionResponse(response);
                }
                else
                {
                    response.Succeeded = false;
                    response.Message = $"Failed to create {objPrefab.name}!";
                    Execution.Instance.SendExecutionResponse(response);
                }
                AssetImporter.CreateObjectsList.RemoveAt(0);
                AssetImporter.SaveLists();
            }
        }
    }
}

#endif