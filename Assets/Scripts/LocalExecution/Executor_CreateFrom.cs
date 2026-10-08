using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.create_from)]
    public class Executor_CreateFrom : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string gameDescPathFile = instruction.parameters.GetString("GameDesc");
            string spacesStr = instruction.parameters.GetString("Spaces");

            if (string.IsNullOrWhiteSpace(gameDescPathFile) || !File.Exists(gameDescPathFile))
                return (false, $"GameDesc file not found: {gameDescPathFile}", null);

            string[] spaceNames = null;
            if (!string.IsNullOrWhiteSpace(spacesStr))
            {
                spaceNames = spacesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < spaceNames.Length; i++)
                    spaceNames[i] = spaceNames[i].Trim();
            }

            var gameDesc = GameDescManager.Instance.DeserializeGameDescFile(gameDescPathFile);
            if (gameDesc == null)
                return (false, "Failed to parse GameDesc file.", null);

            var importAssetsInstruction = new Instruction
            {
                action = T2G.Actions.import_assets,
            };

            foreach (var space in gameDesc.Spaces)
            {
                if (space == null) continue;
                if (spaceNames != null && !spaceNames.Contains(space.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (space.Assets != null)
                {
                    foreach (var assetPair in space.Assets)
                    {
                        GameAsset gameAsset = assetPair.Value;
                        if (gameAsset == null)
                            continue;

                        // Persistent GameAsset is converted back to the transient
                        // Instruction.Asset representation expected by Resolution /
                        // Asset Importer / Executor.
                        string source = !string.IsNullOrWhiteSpace(gameAsset.ImportPath)
                            ? gameAsset.ImportPath
                            : gameAsset.Source;

                        if (string.IsNullOrWhiteSpace(source))
                            continue;

                        var instructionAsset = new Instruction.Asset
                        {
                            desc = gameAsset.Name ?? string.Empty,
                            type = ParseAssetType(gameAsset.Type),
                            source = source
                        };

                        if (importAssetsInstruction.HasAssetSource(instructionAsset))
                            continue;

                        importAssetsInstruction.assets.Add(instructionAsset);
                    }
                }
            }

            Instruction[] instructions;
            if (spaceNames != null && spaceNames.Length > 0)
            {
                var result = new List<Instruction>();
                foreach (var name in spaceNames)
                {
                    var space = gameDesc.Spaces?.Find(s =>
                        s != null && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (space == null)
                    {
                        UnityEngine.Debug.LogWarning($"Space '{name}' not found in GameDesc, skipping.");
                        continue;
                    }
                    var spaceInstructions = GameDescParser.ParseSpaceForInstructions(space);
                    if (spaceInstructions != null)
                    {
                        result.AddRange(spaceInstructions);
                        result.Add(new Instruction
                        {
                            action = T2G.Actions.save_space,
                        });

                    }
                }
                instructions = result.ToArray();
            }
            else
            {
                instructions = GameDescParser.ParseForInstructions(gameDesc);
            }

            if (instructions == null || instructions.Length == 0)
                return (false, "No instructions generated from GameDesc.", null);

            var batchInstructions = new List<Instruction>(instructions);

            if (importAssetsInstruction.assets.Count > 0)
                batchInstructions.Insert(0, importAssetsInstruction);

            await Task.Yield();

            return (true, $"{batchInstructions.Count} instructions are queued to be executed:", batchInstructions);
        }

        private static AssetType ParseAssetType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return AssetType.Unknown;

            AssetType assetType;
            return Enum.TryParse(value, true, out assetType)
                ? assetType
                : AssetType.Unknown;
        }
    }
}
