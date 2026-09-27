using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.open_project)]
    public class CmdTranslator_OpenProject : CmdTranslatorBase
    {
        public override (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            if (args != null)
            {
                foreach (var arg in args)
                {
                    Debug.Log($"[CmdTranslator_OpenProject] arg: {arg.name} = {arg.value}");
                }
            }

            Instruction instruction = new Instruction();
            instruction.action = GetActionName();
            instruction.type = InstructionType.Local;

            string path = Utils.GetParamFromArguments(args, "path").Trim();
            string prjName = Utils.GetParamFromArguments(args, "name").Trim();

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