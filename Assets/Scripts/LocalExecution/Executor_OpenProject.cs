using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.open_project)]
    public class Executor_OpenProject : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string unityEditorPath = Assistant.Instance.Settings.UnityEditorPath;
            if (string.IsNullOrEmpty(unityEditorPath) || !File.Exists(unityEditorPath))
            {
                return (false, "Please setup Unity Editor path before opening a project.", null);
            }

            string projectPath = instruction.parameters.GetString("Path");
            string projectName = instruction.parameters.GetString("ProjectName");

            if (string.IsNullOrEmpty(projectPath) && string.IsNullOrEmpty(projectName) &&
                !string.IsNullOrEmpty(Assistant.Instance.Settings.DefaultUnityProject))
            {
                projectName = Path.GetFileName(Assistant.Instance.Settings.DefaultUnityProject);
                projectPath = Path.GetDirectoryName(Assistant.Instance.Settings.DefaultUnityProject);
            }

            string projectPathName = Path.Combine(projectPath, projectName);

            if (!Directory.Exists(projectPathName))
            {
                return (false, $"Project {projectPathName} was not found.", null);
            }

            var arguments = $"-projectPath {projectPathName}";

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = unityEditorPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                return (false, "Failed to start Unity Editor process.", null);
            }

            bool connected = await CommunicatorClient.Instance.WaitForConnected();

            if (connected)
            {
                Assistant.Instance.Settings.DefaultUnityProject = projectPathName;
                ChatBotUI.Instance.SaveSettings();

                return (true, "Project is opened!", null);
            }
            else
            {
                return (false, "Failed to open the project!", null);
            }
        }
    }
}
