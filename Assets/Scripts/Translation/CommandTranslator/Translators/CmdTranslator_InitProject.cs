using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace T2G.Assistant
{
    [CommandTranslator("init_project")]
    public class CmdTranslator_InitProject : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.type = InstructionType.Local;

            string path = Utils.GetParamFromArguments(args, "path");
            string prjName = Utils.GetParamFromArguments(args, "name");

            if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(prjName) &&
                !string.IsNullOrEmpty(Assistant.Instance.Settings.DefaultUnityProject))
            {
                prjName = Path.GetFileName(Assistant.Instance.Settings.DefaultUnityProject);
                path = Path.GetDirectoryName(Assistant.Instance.Settings.DefaultUnityProject);
            }

            if (string.IsNullOrEmpty(path) || !Utils.IsValidPath(path))
            {
                return (false, null);
            }

            if (string.IsNullOrEmpty(prjName))
            {
                prjName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                path = Path.GetDirectoryName(path);
            }
            instruction.parameters.Add(new Instruction.Parameter("Path", path));
            instruction.parameters.Add(new Instruction.Parameter("ProjectName", prjName));
            return (true, instruction);
        }
    }
}