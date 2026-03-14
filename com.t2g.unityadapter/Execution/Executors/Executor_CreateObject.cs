#if UNITY_EDITOR

using System;
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
        static bool _skipLastResponse = false;

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string name = instruction.parameters.GetString("Name");
            string desc = instruction.desc;
            GameObject newObj = null;

            if (string.Compare(desc, "object", true) == 0)
            {
                newObj = new GameObject(name);
            }
            else if (Utils.IsPrimitiveDesc(desc, out var primitiveType))
            {
                Debug.Log($"Create primitive: {primitiveType.ToString()}");
                newObj = GameObject.CreatePrimitive(primitiveType.Value);
                newObj.name = name;
            }
            else
            {
                AssetImporter.ImportAssets(name, instruction.assets);
                _skipLastResponse = true;
                CreateObjectImpl();
            }

            if (newObj != null)
            {
                Utils.PlaceInFrontOfCamera(newObj);
            }

            return (true, $"{name} was created.", null);
        }

        static bool CreateObject((string name, string targetRelPath) objPrefab, out GameObject gameObj)
        {
            string prefabPath = Path.Combine("Assets", objPrefab.targetRelPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                gameObj = GameObject.Instantiate<GameObject>(prefabAsset);
                gameObj.name = objPrefab.name;
                return true;
            }
            gameObj = null;
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

            while(AssetImporter.CreateObjectsList.Count > 0)
            {
                Response response = new Response(); //Send delayed execution  response to the assistant
                var objPrefab = AssetImporter.CreateObjectsList[0];
                if (CreateObject(objPrefab, out var gameObject))
                {
                    Utils.PlaceInFrontOfCamera(gameObject);
                    Utils.UpdateEditorViews();
                    response.Succeeded = true;
                    response.Message = $"{objPrefab.name} was created.";
                    if (!_skipLastResponse || AssetImporter.CreateObjectsList.Count > 1)
                    {
                        Execution.Instance.SendExecutionResponse(response);
                    }
                }
                else
                {
                    response.Succeeded = false;
                    response.Message = $"Failed to create {objPrefab.name}!";
                    Execution.Instance.SendExecutionResponse(response);
                }
                _skipLastResponse = false;
                AssetImporter.CreateObjectsList.RemoveAt(0);
                AssetImporter.SaveLists();
            }
        }
    }
}

#endif