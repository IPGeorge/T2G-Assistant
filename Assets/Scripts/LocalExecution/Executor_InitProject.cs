using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.init_project)]
    public class Executor_InitProject : ExecutorBase
    {
        static readonly string k_T2G_UnityAdapter_Package = "com.t2g.unityadapter";
        static readonly string k_unity_ugui = "com.unity.ugui";
        static readonly string k_unity_ugui_version = "2.0.0";
        static readonly string k_editor_coroutines = "com.unity.editorcoroutines";
        static readonly string k_editor_coroutines_version = "1.0.0";

        private string _projectPath;
        private string _projectName;
        private string _projectPathName;

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string unityEditorPath = Assistant.Instance.Settings.UnityEditorPath;
            if (string.IsNullOrEmpty(unityEditorPath) || !File.Exists(unityEditorPath))
                return (false, "Please setup Unity Editor path before initializing a project.", null);

            string pluginPath = Assistant.Instance.Settings.T2G_UnityPluginPath;
            if (string.IsNullOrEmpty(pluginPath) || pluginPath.IndexOf(k_T2G_UnityAdapter_Package, StringComparison.OrdinalIgnoreCase) < 0)
                return (false, "Please setup the T2G plugin path for Unity before initializing a project.", null);

            _projectPath = instruction.parameters.GetString("Path");
            _projectName = instruction.parameters.GetString("ProjectName");
            _projectPathName = Path.Combine(_projectPath, _projectName);

            if (!Directory.Exists(_projectPathName))
                return (false, "Project was not found!", null);

            string manifestFilePath = Path.Combine(_projectPathName, "Packages", "manifest.json");
            if (!File.Exists(manifestFilePath))
                return (false, "Failed to open the manifest.json file for project initialization!", null);

            // Retry file operations in case the OS hasn't fully released locks from project creation
            const int maxRetries = 5;
            const int delayMs = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    string json = File.ReadAllText(manifestFilePath);
                    Dependencies dependencies = JsonConvert.DeserializeObject<Dependencies>(json);

                    string absolutePluginPath = Path.GetFullPath(pluginPath);
                    string packagePath = "file:" + absolutePluginPath;
                    string packageName = k_T2G_UnityAdapter_Package;
                    if (!dependencies.DependencyMap.ContainsKey(packageName))
                        dependencies.DependencyMap.Add(packageName, packagePath);

                    if (!dependencies.DependencyMap.ContainsKey(k_unity_ugui))
                        dependencies.DependencyMap.Add(k_unity_ugui, k_unity_ugui_version);

                    if (!dependencies.DependencyMap.ContainsKey(k_editor_coroutines))
                        dependencies.DependencyMap.Add(k_editor_coroutines, k_editor_coroutines_version);

                    json = JsonConvert.SerializeObject(dependencies, Formatting.Indented);
                    File.WriteAllText(manifestFilePath, json);

                    // Launch Unity in batch mode to resolve packages and regenerate the asset database
                    //var arguments = $"-projectPath \"{_projectPathName}\" -quit -batchMode";
                    //try
                    //{
                    //    using var process = new Process();
                    //    process.StartInfo.FileName = unityEditorPath;
                    //    process.StartInfo.Arguments = arguments;
                    //    process.StartInfo.UseShellExecute = false;
                    //    process.StartInfo.CreateNoWindow = true;
                    //    process.Start();
                    //    process.WaitForExit();

                    //    if (process.ExitCode != 0)
                    //        UnityEngine.Debug.LogWarning($"T2G: Package resolution exited with code {process.ExitCode}. The project may need to be opened manually in Unity Editor to fully resolve packages.");
                    //}
                    //catch (Exception e)
                    //{
                    //    UnityEngine.Debug.LogWarning($"T2G: Failed to launch Unity for package resolution: {e.Message}. The project may need to be opened manually.");
                    //}

                    Assistant.Instance.Settings.DefaultUnityProject = _projectPathName;
                    ChatBotUI.Instance.SaveSettings();

                    return (true, "Project has been initialized!", null);
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs);
                }
            }

            return (false, "Failed to initialize project: file is locked by another process.", null);
        }

        public class Dependencies
        {
            [JsonProperty("dependencies")]
            public Dictionary<string, string> DependencyMap { get; set; }
        }
    }
}
