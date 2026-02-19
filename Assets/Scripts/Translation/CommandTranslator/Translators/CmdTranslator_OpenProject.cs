using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace T2G.Assistant
{
    [CommandTranslator("open_project")]
    public class CmdTranslator_OpenProject : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            List<Instruction> instructions = new List<Instruction>();

            Instruction instruction = new Instruction();
            instruction.action = GetType().GetCustomAttribute<CommandTranslatorAttribute>()?.Action;
            instruction.state = Instruction.eState.Local;
            instruction.parameters = new List<ValuePair>();

            string path = Utils.GetParamFromArguments(args, "path");
            string prjName = Utils.GetParamFromArguments(args, "name");

            if (string.IsNullOrEmpty(path) || !Utils.IsValidPath(path))
            {
                return (false, null);
            }

            if (string.IsNullOrEmpty(prjName))
            {
                prjName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                path = Path.GetDirectoryName(path);
            }
            instruction.parameters.Add(new ValuePair("path", path));
            instruction.parameters.Add(new ValuePair("projectName", prjName));
            instructions.Add(instruction);
            return (true, instructions);
        }

    }
}