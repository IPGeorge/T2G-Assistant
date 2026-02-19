using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics;
using UnityEngine;

namespace T2G.Assistant
{
    [LocalExecutor("open_project")]
    public class Executor_OpenProject : ExecutorBase
    {
        private string _projectPath;
        private string _projectName;
        private string _projectPathName;

        private static bool _connected = false;

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string unityEditorPath = Assistant.Instance.Settings.UnityEditorPath;
            if (string.IsNullOrEmpty(unityEditorPath) || !File.Exists(unityEditorPath))
            {
                return (false, "Please setup Unity Editor path before openning a project.", null);
            }

            _projectPath = instruction.parameters.GetString("path");
            _projectName = instruction.parameters.GetString("projectName");
            _projectPathName = Path.Combine(_projectPath, _projectName);

            if (!Directory.Exists(_projectPathName))
            {
                return (false, $"Project {_projectPathName} was not found.", null);
            }

            var arguments = $"-projectPath {_projectPathName}";
            var editorTask = Task.Run(() => StartOpenProjectAsync(arguments, unityEditorPath));
            var editorStarted = await Task.WhenAny(editorTask, Task.Delay(60000)) == editorTask;

            if (!editorStarted)
            {
                return (false, "Timeout waiting for Unity Editor to start.", null);
            }

            _connected = await WaitForConnected();

            if (_connected)
            {
                return (true, $"Project is openned!", null);
            }
            else
            {
                return (true, $"Failed to open the project!", null);
            }
        }

        static async Task<bool> StartOpenProjectAsync(string args, string unityEditorPath)
        {
            return await Task.Run(() =>
            {
                Process process = new Process();
                process.StartInfo.FileName = unityEditorPath;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                try
                {
                    process.Start();

                    // Don't wait for exit here - just start it. Return immediately so we can check connection
                    return true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(e.Message);
                    process?.Dispose();
                    return false;
                }
            });
        }


        static async Awaitable<bool> WaitForConnected(float delaySeconds = 120.0f)
        {
            bool timeout = false;
            DateTime startDT = DateTime.Now;
            while (!CommunicatorClient.Instance.IsConnected)
            {
                if ((DateTime.Now - startDT).TotalSeconds > delaySeconds)
                {
                    timeout = true;
                    break;
                }
                await Task.Delay(1000);
            }
            return !timeout;
        }

    }
}
