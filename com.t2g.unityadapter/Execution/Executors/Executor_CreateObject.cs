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
                Utils.PlaceInFrontOfCamera(_newObj);
                return (true, $"{name} was created.", null);
            }
            else if (Utils.IsPrimitiveDesc(desc, out var primitiveType))
            {
                _newObj = GameObject.CreatePrimitive(primitiveType.Value);
                _newObj.name = name;
                Utils.PlaceInFrontOfCamera(_newObj);
                return (true, $"{name} was created.", null);

            }
            else
            {
                await AssetImporter.ImportAssets(name, instruction.assets);
                CreateObjectImpl();
                return (false, null, null);
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