using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using T2G;
using UnityEngine;

namespace T2G.Assistant
{
    public sealed partial class GameDescManager
    {
        // ============================================================
        // From file (local buffer — never touches Snapshot)
        // ============================================================

        /// <summary>
        /// Loads a GameDesc JSON file into a local buffer and returns
        /// flat instructions to recreate a single space.
        /// Does not modify the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpace(string filePath, string spaceName)
        {
            ValidateArgs(filePath, spaceName);

            var gd = DeserializeGameDescFile(filePath);
            if (gd == null) return null;

            var space = FindSpaceInDesc(gd, spaceName);
            if (space == null)
                throw new InvalidOperationException($"Space '{spaceName}' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        /// <summary>
        /// Loads a GameDesc JSON file into a local buffer and returns
        /// flat instructions for the specified spaces.
        /// If spaceNames is null or empty, returns instructions for ALL spaces.
        /// Does not modify the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string filePath, string[] spaceNames)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath is empty.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("GameDesc file not found.", filePath);
            }

            var gd = DeserializeGameDescFile(filePath);
            if (gd == null) return null;

            if (spaceNames == null || spaceNames.Length == 0)
            {
                return GameDescParser.ParseForInstructions(gd);
            }

            var result = new List<Instruction>();
            foreach (var name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var space = FindSpaceInDesc(gd, name);
                if (space == null)
                {
                    UnityEngine.Debug.LogWarning($"Space '{name}' not found in GameDesc, skipping.");
                    continue;
                }
                var spaceInstructions = GameDescParser.ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Parses a comma-separated space list string.
        /// If null or empty, returns null (meaning "all spaces").
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string filePath, string spaceList = null, int tempreture = 0)
        {
            string[] names = null;

            if (!string.IsNullOrWhiteSpace(spaceList))
            {
                names = spaceList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = names[i].Trim();
                }
            }

            var instructions = GetInstructionsForSpaces(filePath, names);
            return instructions;
        }

        // ============================================================
        // From current Snapshot
        // ============================================================

        /// <summary>
        /// Generates flat instructions for a single space from the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpace(string spaceName)
        {
            EnsureSnapshot();

            var space = FindSpace(spaceName);
            if (space == null)
                throw new InvalidOperationException($"Space '{spaceName}' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        /// <summary>
        /// Generates flat instructions for specified spaces from the current Snapshot.
        /// If spaceNames is null or empty, returns ALL spaces.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces(string[] spaceNames)
        {
            EnsureSnapshot();

            if (spaceNames == null || spaceNames.Length == 0)
            {
                return GameDescParser.ParseForInstructions(Snapshot);
            }

            var result = new List<Instruction>();
            foreach (var name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var space = FindSpace(name);
                if (space == null)
                {
                    UnityEngine.Debug.LogWarning($"Space '{name}' not found in Snapshot, skipping.");
                    continue;
                }
                var spaceInstructions = GameDescParser.ParseSpaceForInstructions(space);
                if (spaceInstructions != null)
                    result.AddRange(spaceInstructions);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Generates flat instructions for ALL spaces from the current Snapshot.
        /// </summary>
        public Instruction[] GetInstructionsForSpaces()
        {
            EnsureSnapshot();
            return GameDescParser.ParseForInstructions(Snapshot);
        }

        // ============================================================
        // Private helpers
        // ============================================================

        private static void ValidateArgs(string filePath, string spaceName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);
        }

        private GameDesc DeserializeGameDescFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var wrapper = JsonConvert.DeserializeObject<GameDescFile>(json, _jsonSettings);
            if (wrapper?.GameDesc == null)
                throw new InvalidOperationException("Invalid file: GameDesc missing.");

            var gd = wrapper.GameDesc;

            if (wrapper.SchemaVersion == null || wrapper.SchemaVersion < 1)
            {
                // Legacy hierarchical format — migrate
                var legacyWrapper = JsonConvert.DeserializeObject<LegacyGameDescFile>(json, _jsonSettings);
                if (legacyWrapper?.GameDesc == null)
                    throw new InvalidOperationException("Invalid file: cannot migrate legacy GameDesc.");
                gd = MigrateFromLegacy(legacyWrapper.GameDesc);
            }

            Normalize(gd);
            return gd;
        }

        private static Space FindSpaceInDesc(GameDesc gd, string spaceName)
        {
            return gd?.Spaces?.Find(s =>
                s != null && string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
