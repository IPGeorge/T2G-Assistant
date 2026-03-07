
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;


namespace T2G
{
    public class AssetImporter
    {
        public static readonly string AssetsToImportListFileName = "ImportAssetList.txt";
        public static readonly string GameObjectsToCreateListFileName = "CreateGameObjectsList.txt";

        static List<string> _importAssetList = new List<string>();
        static List<(string name, string filePath)> _createObjectsList = new List<(string, string)>();
        static GameObject _newGameObject = null;

        public static List<string> ImportAssetList => _importAssetList;
        public static List<(string name, string path)> CreateObjectsList => _createObjectsList;

        public static void ImportAssets(string objName, List<string> assets)
        {
            _newGameObject = null;

            int i = 0;
            foreach(var assetPath in assets)
            {
                if(Path.GetExtension(assetPath).ToLower() == ".prefab")
                {
                    if (i == 0)
                    {
                        _createObjectsList.Add((objName, assetPath));
                    }
                    else
                    {
                        _createObjectsList.Add((objName + "_" + i, assetPath));
                    }
                }
            }
            _importAssetList.AddRange(assets);
            SaveLists();
            ImportAssetsImpl();
        }

        public static void SaveLists()
        {
            string path = Path.Combine(Application.persistentDataPath, AssetsToImportListFileName);
            File.WriteAllLines(path, _importAssetList);
            path = Path.Combine(Application.persistentDataPath, GameObjectsToCreateListFileName);
            string json = JsonConvert.SerializeObject(_createObjectsList);
            File.WriteAllText(path, json);
        }

        public static void LoadLists()
        {
            string path = Path.Combine(Application.persistentDataPath, AssetsToImportListFileName);
            if (File.Exists(path))
            {
                _importAssetList = new List<string>(File.ReadAllLines(path));
            }
            else
            {
                _importAssetList.Clear();
            }
            path = Path.Combine(Application.persistentDataPath, GameObjectsToCreateListFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _createObjectsList = JsonConvert.DeserializeObject<List<(string, string)>>(json);
            }
            else
            {
                _createObjectsList.Clear();
            }
        }

        [InitializeOnLoadMethod]
        static void ImportAssetsImpl()
        {
            LoadLists();

            while(_importAssetList.Count > 0)
            {
                string assetPath = _importAssetList[0];
                string targetPath = Path.Combine(EditorApplication.applicationContentsPath, assetPath);
                if (!File.Exists(targetPath))  //TODO: version number to override
                {
                    string sourcePath = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, assetPath);
                    if (File.Exists(sourcePath))
                    {
                        string targetDirectory = Path.GetFullPath(targetPath);
                        if(!Directory.Exists(targetDirectory))
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }
                        File.Copy(sourcePath, targetPath, true);
                    }
                }
                _importAssetList.RemoveAt(0);
                SaveLists();
            }
        }
    }
}