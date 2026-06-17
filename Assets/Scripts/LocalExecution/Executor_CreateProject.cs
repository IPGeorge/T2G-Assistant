using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.create_project)]
    public class Executor_CreateProject : ExecutorBase
    {
        private Process _process;
        private string _projectPath;
        private string _projectName; 
        private string _projectPathName;

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string unityEditorPath = Assistant.Instance.Settings.UnityEditorPath;
            if (string.IsNullOrEmpty(unityEditorPath) || !File.Exists(unityEditorPath))
                return (false, "Please setup Unity Editor path before creating a new project.", null);

            string pluginPath = Assistant.Instance.Settings.T2G_UnityPluginPath;
            if (string.IsNullOrEmpty(pluginPath))
                return (false, "Please setup the T2G plugin path for Unity before creating a new project.", null);

            _projectPath = instruction.parameters.GetString("path");
            _projectName = instruction.parameters.GetString("projectName");
            _projectPathName = Path.Combine(_projectPath, _projectName);

            try
            {
                if (Directory.Exists(_projectPathName))
                    Directory.Delete(_projectPathName, true);

                if (!Directory.Exists(_projectPath))
                    Directory.CreateDirectory(_projectPath);

                var arguments = $"-batchMode -createproject {_projectPathName} -quit";

                _process = new Process();
                _process.StartInfo.FileName = unityEditorPath;
                _process.StartInfo.Arguments = arguments;
                _process.Start();
                _process.WaitForExit();

                // Wait for OS to fully release file handles from the exited Unity process
                await Task.Delay(3000);

                if (_process.ExitCode == 0)
                {
                    var additionals = new List<Instruction>
                    {
                        new Instruction
                        {
                            action = "init_project",
                            state = Instruction.eState.Local,
                            parameters = new List<ValuePair>
                            {
                                new ValuePair("path", _projectPath),
                                new ValuePair("projectName", _projectName)
                            }
                        }
                    };

                    Assistant.Instance.Settings.DefaultUnityProject = _projectPathName;
                    ChatBotUI.Instance.SaveSettings();

                    return (true, $"Project {_projectPathName} was created!", additionals);
                }
                else
                {
                    return (false, $"Failed to create project! Exit Code: {_process.ExitCode}", null);
                }
            }
            catch (Exception e)
            {
                return (false, "Failed to create project: " + e.Message, null);
            }
            finally
            {
                _process?.Close();
            }
        }
    }
}
