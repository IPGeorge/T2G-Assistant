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
            _newObj = null;

            if (string.Compare(desc, "object", true) == 0)
            {
                _newObj = new GameObject(name);
            }
            else if(string.Compare(desc, "camera", true) == 0 || 
                desc.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _newObj = new GameObject(name);
                Camera camera = _newObj.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
            }
            else if(string.Compare(desc, "light", true) == 0 ||
                desc.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _newObj = new GameObject(name);
                Light light = _newObj.AddComponent<Light>();
                light.type = Utils.GetLightTypeFromDesc(desc);
                light.color = Color.white;
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
                await AssetImporter.ImportAssets(name, instruction.assets);
                CreateObjectImpl();
                return (false, null, null);
            }
            else
            {
                Utils.PlaceInFrontOfCamera(_newObj);
                return (true, $"{name} was created.", null);
            }
        }

        static bool CreateObject((string name, string targetRelPath) objPrefab)
        {
            string prefabPath = Path.Combine("Assets", objPrefab.targetRelPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                _newObj = GameObject.Instantiate<GameObject>(prefabAsset);
                _newObj.name = objPrefab.name;
                return true;
            }
            _newObj = null;
            return false;
        }

        [InitializeOnLoadMethod]
        static async void CreateObjectImpl()
        {
            AssetImporter.LoadLists();

            while(AssetImporter.ImportAssetList.Count > 0)
            {
                await Task.Yield();
            }

            while (AssetImporter.CreateObjectsList.Count > 0)
            {
                Response response = new Response(); //Send delayed execution  response to the assistant
                var objPrefab = AssetImporter.CreateObjectsList[0];
                if (CreateObject(objPrefab))
                {
                    Utils.PlaceInFrontOfCamera(_newObj);
                    Utils.UpdateEditorViews();
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