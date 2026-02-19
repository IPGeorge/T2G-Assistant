using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO;
using Newtonsoft.Json;

namespace T2G.Assistant
{
    [LocalExecutor("init_project")]
    public class Executor_InitProject : ExecutorBase
    {
        static readonly string k_T2G_UnityAdapter_Package = "com.t2g.unityadapter";
        static readonly string k_unity_ugui = "com.unity.ugui";
        static readonly string k_unity_ugui_version = "2.0.0";
        static readonly string k_editor_coroutines = "com.unity.editorcoroutines";
        static readonly string k_editor_coroutines_version = "1.0.0";

        private Process _process;
        private EventHandler _eventHandler;
        private string _projectPath;
        private string _projectName;
        private string _projectPathName;

        public override Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            _tcs = new TaskCompletionSource<(bool, string, List<Instruction>)>();

            string unityEditorPath = Assistant.Instance.Settings.UnityEditorPath;
            if (string.IsNullOrEmpty(unityEditorPath) || !File.Exists(unityEditorPath))
            {
                _tcs.SetResult((false, "Please setup Unity Editor path before initializing a project.", null));
                return _tcs.Task;
            }

            string pluginPath = Assistant.Instance.Settings.T2G_UnityPluginPath;
            if (string.IsNullOrEmpty(pluginPath) || pluginPath.IndexOf(k_T2G_UnityAdapter_Package) < 0)
            {
                _tcs.SetResult((false, "Please setup the T2G plugin path for Unity before initializing a project.", null));
                return _tcs.Task;
            }

            _projectPath = instruction.parameters.GetString("path");
            _projectName = instruction.parameters.GetString("projectName");
            _projectPathName = Path.Combine(_projectPath, _projectName);

            if (!Directory.Exists(_projectPathName))
            {
                _tcs.SetResult((false, $"Project was not found!", null));
                return _tcs.Task;
            }

            string manifestFilePath = Path.Combine(_projectPathName, "Packages", "manifest.json");
            if (File.Exists(manifestFilePath))
            {
                string json = File.ReadAllText(manifestFilePath);
                Dependencies dependencies = JsonConvert.DeserializeObject<Dependencies>(json);

                string packagePath = "file:" + pluginPath;
                string packageName = k_T2G_UnityAdapter_Package;
                if (!dependencies.DependencyMap.ContainsKey(packageName))
                {
                    dependencies.DependencyMap.Add(packageName, packagePath);
                }

                if (!dependencies.DependencyMap.ContainsKey(k_unity_ugui))
                {
                    dependencies.DependencyMap.Add(k_unity_ugui, k_unity_ugui_version);
                }

                if (!dependencies.DependencyMap.ContainsKey(k_unity_ugui))
                {
                    dependencies.DependencyMap.Add(k_unity_ugui, k_unity_ugui_version);
                }

                if (!dependencies.DependencyMap.ContainsKey(k_editor_coroutines))
                {
                    dependencies.DependencyMap.Add(k_editor_coroutines, k_editor_coroutines_version);
                }

                json = JsonConvert.SerializeObject(dependencies, Formatting.Indented);
                File.WriteAllText(manifestFilePath, json);
                
                _tcs.SetResult((true, "Project has been initialized!", null));
            }
            else
            {
                _tcs.SetResult((false, "Failed to open the manifest.json file for project initialization!", null));
            }

            return _tcs.Task;
        }

        public class Dependencies
        {
            [JsonProperty("dependencies")]
            public Dictionary<string, string> DependencyMap { get; set; }
        }
    }
}
