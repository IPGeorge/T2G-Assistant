using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Threading.Tasks;

namespace T2G.Assistant
{
    [CommandTranslator(T2G.Actions.import_assets)]
    public class CmdTranslator_ImportAssets : CmdTranslatorBase
    {
        public override (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            if (args == null || args.Length <= 0)
            {
                return (false, null);
            }

            Instruction instruction = new Instruction()
            {
                action = GetActionName(),
                state = Instruction.eState.Resolved,
                assets = new List<string>()
            };

            var arg = Utils.GetParamFromArguments(args, "assets");
            var assetArr = arg.Split(",");
            foreach(var asset in assetArr)
            {
                instruction.assets.Add(asset.Trim('"'));
            }

            List<Instruction> instructions = new List<Instruction>() { instruction };

            return (true, instructions);
        }
    }
}