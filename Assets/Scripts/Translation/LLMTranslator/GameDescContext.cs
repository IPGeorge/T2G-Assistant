using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    /// <summary>
    /// Provides GameDesc context for LLM-based instruction translation.
    ///
    /// CurrentGameDescJson:
    ///     Authoritative state of the currently edited game.
    ///
    /// ReferenceGameDescJsons:
    ///     Previously successful game representations used
    ///     as examples for new game creation.
    ///
    /// This class only carries contextual information.
    /// It does not modify GameDesc or execute instructions.
    /// </summary>
    [Serializable]
    public class GameDescContext
    {
        /// <summary>
        /// JSON representation of the current game.
        /// Null when no current GameDesc is available.
        /// </summary>
        public string CurrentGameDescJson { get; set; }

        /// <summary>
        /// JSON representations of reference games.
        /// May be empty when no references are selected.
        /// </summary>
        public List<string> ReferenceGameDescJsons { get; set; } = new List<string>();

        /// <summary>
        /// Whether current project context is available.
        /// </summary>
        public bool HasCurrentGameDesc => !string.IsNullOrWhiteSpace(CurrentGameDescJson);

        /// <summary>
        /// Whether reference examples are available.
        /// </summary>
        public bool HasReferenceGameDescs => ReferenceGameDescJsons != null && ReferenceGameDescJsons.Count > 0;

        /// <summary>
        /// Whether any GameDesc context is available.
        /// </summary>
        public bool HasContext =>
            HasCurrentGameDesc || HasReferenceGameDescs;

        /// <summary>
        /// Creates an empty context.
        /// </summary>
        public GameDescContext()
        {
        }

        /// <summary>
        /// Creates a context containing the current GameDesc.
        /// </summary>
        public GameDescContext(string currentGameDescJson)
        {
            CurrentGameDescJson = currentGameDescJson;
        }

        /// <summary>
        /// Adds a reference GameDesc.
        /// </summary>
        public void AddReference(string gameDescJson)
        {
            if (string.IsNullOrWhiteSpace(gameDescJson))
                return;

            ReferenceGameDescJsons.Add(gameDescJson);
        }

        /// <summary>
        /// Removes all reference GameDescs.
        /// Does not affect the current GameDesc.
        /// </summary>
        public void ClearReferences()
        {
            ReferenceGameDescJsons.Clear();
        }
    }
}