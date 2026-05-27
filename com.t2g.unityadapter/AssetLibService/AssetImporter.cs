
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;


namespace T2G
{
    public class AssetImporter
    {
        public static readonly string AssetsToImportListFileName = "ImportAssetList.txt";
        public static readonly string GameObjectsToCreateListFileName = "CreateGameObjectsList.txt";

        static List<(string sourcePath, string targetRelPath)> _importAssetList = new List<(string, string)>();
        static List<(string name, string targetRelPath, string position)> _createObjectsList = new List<(string, string, string)>();

        public static List<(string sourcePath, string targetRelPath)> ImportAssetList => _importAssetList;
        public static List<(string name, string targetRelPath, string position)> CreateObjectsList => _createObjectsList;

        public static async Awaitable ImportAssets(string objName, List<string> assets, Vector3? position)
        {
            if (position.HasValue)
            {
                var pos = position.Value;
                _createObjectsList.Add((objName, assets[1], string.Format("({0},{1},{2})", pos.x, pos.y, pos.z)));
            }
            else
            {
                _createObjectsList.Add((objName, assets[1], string.Empty));
            }

            _importAssetList.Add((assets[0], assets[1]));
            SaveLists();
            await SimImportAssetsImpl();
        }

        public static async Awaitable<int> BeginImportScripts(List<string> scriptPaths)
        {
            EditorApplication.LockReloadAssemblies();
            int importedCount = 0;
            foreach (var scriptPath in scriptPaths)
            {
                string source = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, scriptPath);
                string target = Path.Combine(Application.dataPath, scriptPath);
                string targetDir = Path.GetDirectoryName(target);
                if (File.Exists(source))
                {
                    if(!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(source, target, true);
                    importedCount++;
                }
            }
            await Task.Yield();
            return importedCount;
        }

        public static async Awaitable EndImportScripts(string componentName = null)
        {
            EditorApplication.UnlockReloadAssemblies();
            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();

            if (componentName != null)
            {
                bool isValidComponentName = false;
                while (!isValidComponentName) //Wait for InitializeOnLoadMethod is triggered
                {
                    await Task.Delay(100);
                    isValidComponentName = Utils.IsValidComponentName(componentName);
                }
            }
        }

        public static void SaveLists()
        {
            string json;
            string path = Path.Combine(Application.persistentDataPath, AssetsToImportListFileName);
            json = JsonConvert.SerializeObject(_importAssetList);
            File.WriteAllText(path, json);
            path = Path.Combine(Application.persistentDataPath, GameObjectsToCreateListFileName);
            json = JsonConvert.SerializeObject(_createObjectsList);
            File.WriteAllText(path, json);
        }

        public static void LoadLists()
        {
            string path = Path.Combine(Application.persistentDataPath, AssetsToImportListFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var tmp = JsonConvert.DeserializeObject<List<(string, string)>>(json);
                _importAssetList = tmp ?? _importAssetList;
            }
            else
            {
                _importAssetList.Clear();
            }
            path = Path.Combine(Application.persistentDataPath, GameObjectsToCreateListFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var tmp = JsonConvert.DeserializeObject<List<(string, string, string)>>(json);
                _createObjectsList = tmp ?? _createObjectsList;
            }
            else
            {
                _createObjectsList.Clear();
            }
        }

        [InitializeOnLoadMethod]
        static async Awaitable SimImportAssetsImpl()
        {
            LoadLists();

            while(_importAssetList.Count > 0)
            {
                var assetPaths = _importAssetList[0];
                var editorAssetsPath = Application.dataPath;
                string targetPath = Path.Combine(editorAssetsPath, assetPaths.targetRelPath);
                if (!File.Exists(targetPath) &&
                    Execution.Instance.Settings.AssetLibraryRootPath != null)  //TODO: version number to override
                {
                    string sourcePath = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, assetPaths.sourcePath);
                    if (File.Exists(sourcePath))
                    {
                        EditorApplication.LockReloadAssemblies();

                        string extension = Path.GetExtension(sourcePath);
                        if (string.Compare(extension, ".unitypackage", true) == 0 ||
                            extension.IndexOf("unitypackage") > 0)
                        {
                            bool isCompleted = false;
                            AssetDatabase.importPackageCompleted += (packageName) => { isCompleted = true; };

                            AssetDatabase.ImportPackage(sourcePath, false);

                            int maxAttempts = 3000; // 300 seconds (3000 * 100ms)
                            int attempts = 0;

                            while (!isCompleted && attempts < maxAttempts)
                            {
                                await Task.Delay(100); // 100ms delay
                                attempts++;
                            }

                            await Task.Delay(500);
                            AssetDatabase.Refresh();
                        }
                        else
                        {
                            string targetDirectory = Path.GetDirectoryName(targetPath);
                            if (!Directory.Exists(targetDirectory))
                            {
                                Directory.CreateDirectory(targetDirectory);
                            }
                            File.Copy(sourcePath, targetPath, true);
                        }

                        EditorApplication.UnlockReloadAssemblies();
                        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
                    }
                }
                _importAssetList.RemoveAt(0);
                SaveLists();
            }
        }
    }
}