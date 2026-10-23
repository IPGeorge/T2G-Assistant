#if UNITY_EDITOR

using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;

namespace T2G
{
    [Executor(Actions.import_assets)]
    public class Executor_ImportAssets : ExecutorBase
    {
        public override async Task<(bool succeeded, 
            string message,
            List<Response.Result> results, 
            List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            if(instruction.assets == null || instruction.assets.Count <= 0)
            {
                return (false, "0 assets need to be imported.", null, null);
            }

            AssetImporter.SetPendingImportInstructionId(instruction.id);

            int count = await AssetImporter.BeginImportAssets(instruction.assets);
            await AssetImporter.EndImportAssets();

            // If execution reached this point no domain reload interrupted the
            // import, so the delayed response above will not be sent again.
            AssetImporter.ClearPendingImportInstructionId();

            return (true, $"{count} assets were imported!", null, null);
        }
    }
}

#endif