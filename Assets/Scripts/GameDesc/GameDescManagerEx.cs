using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using T2G;
using UnityEngine;

namespace T2G.Assistant
{
    /// <summary>
    /// File loading and GameDesc-to-instruction regeneration extensions for
    /// the final GameDescManager. The main GameDescManager must be declared partial.
    /// </summary>
    public partial class GameDescManager
    {
        private static readonly JsonSerializerSettings GameDescJsonSettings =
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };

        // ============================================================
        // From file - local buffer; does not replace CurrentGameDesc.
        // ============================================================

        public Instruction[] GetInstructionsForSpace(string filePath, string spaceName)
        {
            ValidateArgs(filePath, spaceName);

            GameDesc gameDesc = DeserializeGameDescFile(filePath);
            Space space = FindSpaceInDesc(gameDesc, spaceName);

            if (space == null)
                throw new InvalidOperationException("Space '" + spaceName + "' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        public Instruction[] GetInstructionsForSpaces(string filePath, string[] spaceNames)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);

            GameDesc gameDesc = DeserializeGameDescFile(filePath);

            if (spaceNames == null || spaceNames.Length == 0)
                return GameDescParser.ParseForInstructions(gameDesc);

            var result = new List<Instruction>();
            foreach (string name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                Space space = FindSpaceInDesc(gameDesc, name.Trim());
                if (space == null)
                {
                    Debug.LogWarning("Space '" + name + "' not found in GameDesc, skipping.");
                    continue;
                }

                Instruction[] instructions = GameDescParser.ParseSpaceForInstructions(space);
                if (instructions != null)
                    result.AddRange(instructions);
            }

            return result.ToArray();
        }

        public Instruction[] GetInstructionsForSpaces(
            string filePath,
            string spaceList = null,
            int tempreture = 0)
        {
            string[] names = null;

            if (!string.IsNullOrWhiteSpace(spaceList))
            {
                names = spaceList.Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < names.Length; i++)
                    names[i] = names[i].Trim();
            }

            return GetInstructionsForSpaces(filePath, names);
        }

        // ============================================================
        // From CurrentGameDesc.
        // ============================================================

        public Instruction[] GetInstructionsForSpace(string spaceName)
        {
            EnsureCurrentGameDesc();

            Space space = FindSpaceByName(spaceName);
            if (space == null)
                throw new InvalidOperationException("Space '" + spaceName + "' not found.");

            return GameDescParser.ParseSpaceForInstructions(space);
        }

        public Instruction[] GetInstructionsForSpaces(string[] spaceNames)
        {
            EnsureCurrentGameDesc();

            if (spaceNames == null || spaceNames.Length == 0)
                return GameDescParser.ParseForInstructions(CurrentGameDesc);

            var result = new List<Instruction>();
            foreach (string name in spaceNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                Space space = FindSpaceByName(name.Trim());
                if (space == null)
                {
                    Debug.LogWarning("Space '" + name + "' not found in CurrentGameDesc, skipping.");
                    continue;
                }

                Instruction[] instructions = GameDescParser.ParseSpaceForInstructions(space);
                if (instructions != null)
                    result.AddRange(instructions);
            }

            return result.ToArray();
        }

        public Instruction[] GetInstructionsForSpaces()
        {
            EnsureCurrentGameDesc();
            return GameDescParser.ParseForInstructions(CurrentGameDesc);
        }

        // ============================================================
        // Serialization helpers.
        // ============================================================

        public GameDesc DeserializeGameDescFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);

            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Invalid file: GameDesc JSON is empty.");

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Invalid GameDesc JSON.", ex);
            }

            GameDesc gameDesc = null;

            // Supports both the final direct GameDesc JSON and the older wrapper
            // shape { SchemaVersion, Context, GameDesc } without depending on
            // legacy wrapper classes.
            JToken gameDescToken = GetPropertyIgnoreCase(root, "GameDesc");
            if (gameDescToken != null && gameDescToken.Type == JTokenType.Object)
            {
                gameDesc = gameDescToken.ToObject<GameDesc>(
                    JsonSerializer.Create(GameDescJsonSettings));
            }
            else if (LooksLikeFlatGameDesc(root))
            {
                gameDesc = root.ToObject<GameDesc>(
                    JsonSerializer.Create(GameDescJsonSettings));
            }

            if (gameDesc == null)
            {
                // HumanGameDesc uses Objects as arrays rather than dictionaries.
                HumanGameDesc human = root.ToObject<HumanGameDesc>(
                    JsonSerializer.Create(GameDescJsonSettings));

                if (human != null && human.Spaces != null)
                    gameDesc = GameDescConverter.FromHuman(human);
            }

            if (gameDesc == null)
                throw new InvalidOperationException("Invalid file: GameDesc missing.");

            NormalizeLoadedGameDesc(gameDesc);
            return gameDesc;
        }

        public string SerializeGameDesc(GameDesc gameDesc, bool humanReadable = false)
        {
            if (gameDesc == null)
                throw new ArgumentNullException(nameof(gameDesc));

            object value = humanReadable
                ? (object)GameDescConverter.ToHuman(gameDesc)
                : gameDesc;

            return JsonConvert.SerializeObject(value, GameDescJsonSettings);
        }

        private static void ValidateArgs(string filePath, string spaceName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.");
            if (string.IsNullOrWhiteSpace(spaceName))
                throw new ArgumentException("spaceName is empty.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("GameDesc file not found.", filePath);
        }

        private void EnsureCurrentGameDesc()
        {
            if (CurrentGameDesc == null)
                throw new InvalidOperationException("CurrentGameDesc has not been initialized.");
        }

        private static Space FindSpaceInDesc(GameDesc gameDesc, string spaceName)
        {
            if (gameDesc == null || gameDesc.Spaces == null || string.IsNullOrWhiteSpace(spaceName))
                return null;

            return gameDesc.Spaces.Find(s =>
                s != null &&
                string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeFlatGameDesc(JObject root)
        {
            return GetPropertyIgnoreCase(root, "Spaces") != null ||
                   GetPropertyIgnoreCase(root, "ProjectName") != null;
        }

        private static JToken GetPropertyIgnoreCase(JObject obj, string name)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return null;
        }

        private static void NormalizeLoadedGameDesc(GameDesc gameDesc)
        {
            gameDesc.Spaces = gameDesc.Spaces ?? new List<Space>();
            gameDesc.InstructionHistory =
                gameDesc.InstructionHistory ?? new List<InstructionRecord>();

            foreach (Space space in gameDesc.Spaces)
            {
                if (space == null) continue;

                if (string.IsNullOrWhiteSpace(space.Id))
                    space.Id = Guid.NewGuid().ToString();

                space.Properties = space.Properties ?? new List<PropertyDesc>();
                space.Components = space.Components ?? new List<Component>();
                space.Objects = space.Objects ??
                    new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
                space.Assets = space.Assets ??
                    new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);

                foreach (Object obj in space.Objects.Values)
                {
                    if (obj == null) continue;
                    if (string.IsNullOrWhiteSpace(obj.Id))
                        obj.Id = Guid.NewGuid().ToString();

                    obj.Tags = obj.Tags ?? new List<string>();
                    obj.Roles = obj.Roles ?? new List<string>();
                    obj.Relationships = obj.Relationships ?? new List<Relationship>();
                    obj.Properties = obj.Properties ?? new List<PropertyDesc>();
                    obj.Assets = obj.Assets ?? new List<AssetRef>();
                    obj.Components = obj.Components ?? new List<Component>();
                    obj.RebuildPropertyMap();
                }

                space.RebuildNameIndex();
                space.RebuildPropertyMap();
            }
        }
    }
}
