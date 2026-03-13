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
                if(AssetImporter.CreateObjectsList.Count > 0 && 
                    !CreateObject((name, AssetImporter.CreateObjectsList[0].path), out newObj))
                {
                    AssetImporter.CreateObjectsList.RemoveAt(0);
                    AssetImporter.SaveLists();
                    return (false, $"Failed create {name}!", null);
                }
            }

            if (newObj != null)
            {
                Utils.PlaceInFontOfCamera(newObj);
            }

            return (true, $"{name} was created.", null);
        }

        static bool CreateObject((string name, string path) objPrefab, out GameObject gameObj)
        {
            string prefabPath = Path.Combine("Assets", objPrefab.path);
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
        static void CreateObjectImpl()
        {
            AssetImporter.LoadLists();

            if(AssetImporter.ImportAssetList.Count > 0)
            {
                return;
            }

            while(AssetImporter.CreateObjectsList.Count > 0)
            {
                Response response = new Response(); //Send delayed execution  response to the assistant
                var objPrefab = AssetImporter.CreateObjectsList[0];
                if (CreateObject(objPrefab, out var gameObject))
                {
                    Utils.PlaceInFontOfCamera(gameObject);
                    Utils.UpdateEditorViews();
                    response.Succeeded = true;
                    response.Message = $"{objPrefab.name} was created.";
                }
                else
                {
                    response.Succeeded = false;
                    response.Message = $"Failed to create {objPrefab.name}!";
                }
                Execution.Instance.SendExecutionResponse(response);
                AssetImporter.CreateObjectsList.RemoveAt(0);
                AssetImporter.SaveLists();
            }
        }
    }
}

#endif