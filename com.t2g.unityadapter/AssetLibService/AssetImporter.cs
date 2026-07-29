#if UNITY_EDITOR

using System;
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
        public static readonly string PackagesToImportListFileName = "ImportPackagesList.txt";


        static List<(string sourcePath, string targetRelPath)> _importAssetList = new List<(string, string)>();
        static List<(string name, string targetRelPath, string position)> _createObjectsList = new List<(string, string, string)>();

        public static List<(string sourcePath, string targetRelPath)> ImportAssetList => _importAssetList;
        public static List<(string name, string targetRelPath, string position)> CreateObjectsList => _createObjectsList;

        static List<string> _importPackagesList = new List<string>();

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

        static void SaveImportPackagesList()
        {
            string json;
            string path = Path.Combine(Application.persistentDataPath, PackagesToImportListFileName);
            json = JsonConvert.SerializeObject(_importPackagesList);
            File.WriteAllText(path, json);
        }

        static void LoadImportPackagesList()
        {
            string path = Path.Combine(Application.persistentDataPath, PackagesToImportListFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var tmp = JsonConvert.DeserializeObject<List<string>>(json);
                _importPackagesList = tmp ?? _importPackagesList;
            }
            else
            {
                _importPackagesList.Clear();
            }
        }

        public static async Awaitable<int> BeginImportAssets(List<string> assets)
        {
            int importCount = 0;

            _importPackagesList.Clear();

            EditorApplication.LockReloadAssemblies();
            try
            {
                foreach (var asset in assets)
                {
                    if (string.IsNullOrWhiteSpace(asset))
                    {
                        continue;
                    }

                    string[] assetArray = asset.Split(",");
                    foreach (var assetSource in assetArray)
                    {
                        if (string.IsNullOrWhiteSpace(assetSource) ||
                            assetSource.IndexOf(".prefab", System.StringComparison.OrdinalIgnoreCase) > 0)
                        {
                            continue;
                        }

                        string sourcePath = Path.Combine(Execution.Instance.Settings.AssetLibraryRootPath, assetSource);

                        if (assetSource.IndexOf(".unitypackage", System.StringComparison.OrdinalIgnoreCase) > 0)
                        {
                            _importPackagesList.Add(sourcePath);
                            importCount++;
                            continue;
                        }
                        
                        string target = Path.Combine(Application.dataPath, asset);
                        string targetDir = Path.GetDirectoryName(target);
                        if (File.Exists(sourcePath))
                        {
                            if (!Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }
                            File.Copy(sourcePath, target, true);
                            CommunicatorServerEditor.AddConsoleText($"Imported {Path.GetFileName(target)}.");
                            importCount++;
                        }
                    }
                }
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }

            await Task.Yield();
            return importCount;
        }

        public static async Awaitable EndImportAssets()
        {
            SaveImportPackagesList();
            EditorApplication.UnlockReloadAssemblies();
            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
            await Utils.WaitForUnityIdle();
            await ProcessPackageImportsImpl();
        }

        [InitializeOnLoadMethod]
        static async Awaitable ProcessPackageImportsImpl()
        {
            LoadImportPackagesList();

            while (_importPackagesList.Count > 0)
            {
                string packagePath = _importPackagesList[0];
                _importPackagesList.RemoveAt(0);
                SaveImportPackagesList();
                CommunicatorServerEditor.AddConsoleText($"Importing {Path.GetFileName(packagePath)} ...");
                await ImportUnityPackage(packagePath);
            }

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
            string path = Path.Combine(Application.persistentDataPath, AssetsToImportListFileName);
            string json = JsonConvert.SerializeObject(_importAssetList);
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

                _importAssetList.RemoveAt(0);
                SaveLists();

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
                            EditorApplication.UnlockReloadAssemblies();
                            await ImportUnityPackage(sourcePath);
                            EditorApplication.LockReloadAssemblies();
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
                        AssetDatabase.Refresh();
                    }
                }
            }
        }

        private static async Awaitable<bool> ImportUnityPackage(string sourcePath)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

            AssetDatabase.ImportPackageCallback onCompleted = null;
            AssetDatabase.ImportPackageFailedCallback onFailed = null;
            AssetDatabase.ImportPackageCallback onCancelled = null;

            onCompleted = (name) => { Unsubscribe(); tcs.TrySetResult(true); };
            onFailed = (name, err) => { Unsubscribe(); tcs.TrySetResult(false); };
            onCancelled = (name) => { Unsubscribe(); tcs.TrySetResult(false); };

            void Unsubscribe()
            {
                AssetDatabase.importPackageCompleted -= onCompleted;
                AssetDatabase.importPackageFailed -= onFailed;
                AssetDatabase.importPackageCancelled -= onCancelled;
            }

            AssetDatabase.importPackageCompleted += onCompleted;
            AssetDatabase.importPackageFailed += onFailed;
            AssetDatabase.importPackageCancelled += onCancelled;

            try
            {
                AssetDatabase.ImportPackage(sourcePath, false);
                return await tcs.Task;
            }
            finally
            {
                Unsubscribe();
            }
        }
    }
}

#endif