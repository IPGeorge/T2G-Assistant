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
            string desc = instruction.parameters.GetString("Desc");
            GameObject newObj = null;

            if (string.IsNullOrEmpty(desc))
            {
                newObj = new GameObject(name);
            }
            else if (IsPrimitive(desc, out var primitiveType))
            {
                newObj = GameObject.CreatePrimitive(primitiveType.Value);
                newObj.name = name;
            }
            else
            {
                AssetImporter.ImportAssets(name, instruction.assets);
                if(AssetImporter.CreateObjectsList.Count > 0 && 
                    !CreateObject((name, AssetImporter.CreateObjectsList[0].path)))
                {
                    AssetImporter.CreateObjectsList.RemoveAt(0);
                    AssetImporter.SaveLists();
                    return (false, $"Failed create {name}!", null);
                }
            }

            return (true, $"{name} was created.", null);
        }

        bool IsPrimitive(string desc, out PrimitiveType? primitiveType)
        {
            foreach (PrimitiveType enumType in Enum.GetValues(typeof(PrimitiveType)))
            {
                string typeName = enumType.ToString();
                if(string.Compare(typeName, desc, true) == 0)
                {
                    primitiveType = enumType;
                    return true;
                }
            }
            primitiveType = null;
            return false;
        }

        static bool CreateObject((string name, string path) objPrefab)
        {
            string prefabPath = Path.Combine("Assets", objPrefab.path);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                GameObject gameObj = GameObject.Instantiate<GameObject>(prefabAsset);
                gameObj.name = objPrefab.name;
                return true;
            }
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
                if (CreateObject(objPrefab))
                {
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