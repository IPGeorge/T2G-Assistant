using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor("create_project")]
    public class Executor_CreateProject : ExecutorBase
    {
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
                _tcs.SetResult((false, "Please setup Unity Editor path before creating a new project.", null));
                return _tcs.Task;
            }

            string pluginPath = Assistant.Instance.Settings.T2G_UnityPluginPath;
            if (string.IsNullOrEmpty(pluginPath))
            {
                _tcs.SetResult((false, "Please setup the T2G plugin path for Unity before creating a new project.", null));
                return _tcs.Task;
            }

            _projectPath = instruction.parameters.GetString("path");
            _projectName = instruction.parameters.GetString("projectName");
            _projectPathName = Path.Combine(_projectPath, _projectName);

            try
            {
                if (Directory.Exists(_projectPathName))
                {
                    Directory.Delete(_projectPathName, true);  //delete the old project
                }

                if (!Directory.Exists(_projectPath))
                {
                    Directory.CreateDirectory(_projectPath);
                }

                var arguments = $"-batchMode -createproject {_projectPathName} -quit";

                _eventHandler = new EventHandler(ProcessExitedHandler);
                _process = new Process();
                _process.Exited += _eventHandler;
                _process.StartInfo.FileName = unityEditorPath;
                _process.StartInfo.Arguments = arguments;
                _process.EnableRaisingEvents = true;
                _process.Start();
                _process.WaitForExit();
                _process.Close();
                _process.Exited -= _eventHandler;

                Assistant.Instance.CreateNewProject(_projectName, _projectPathName);
            }
            catch (Exception e)
            {
                if (_process != null)
                {
                    _process.Close();
                    _process.Exited -= _eventHandler;
                }
                _tcs.SetResult((false, "Failed! \n" + e.Message, null));
            }

            return _tcs.Task;
        }

        void ProcessExitedHandler(object sender, EventArgs args)
        {
            if (_process.ExitCode == 0)
            {
                List<Instruction> additionals = new List<Instruction>();
                Instruction initProjectInstruction = new Instruction();
                initProjectInstruction.action = "init_project";
                initProjectInstruction.state = Instruction.eState.Local;
                initProjectInstruction.parameters = new List<ValuePair>();
                initProjectInstruction.parameters.Add(new ValuePair("path", _projectPath));
                initProjectInstruction.parameters.Add(new ValuePair("projectName", _projectName));
                additionals.Add(initProjectInstruction); 
                _tcs.SetResult((true, $"Project {_projectPathName} was created!", additionals));
            }
            else
            {
                _tcs.SetResult((false, $"Failed! Exit Code: {_process.ExitCode}", null));
            }
        }
    }
}
