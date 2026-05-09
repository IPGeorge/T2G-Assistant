#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace T2G
{
    [Executor(T2G.Actions.rename_space)]
    public class Executor_RenameSpace : ExecutorBase
    {
        private readonly string k_SpacesDirectory = "Spaces";

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string spacesPath = Path.Combine(Application.dataPath, k_SpacesDirectory);
            if (!Directory.Exists(spacesPath))
            {
                Directory.CreateDirectory(spacesPath);
            }

            string newSpaceName = instruction.parameters.GetString("spaceName");
            if (string.IsNullOrEmpty(newSpaceName))
            {
                return (false, "Can't rename space with an empty name", null);
            }
            var activeScene = EditorSceneManager.GetActiveScene();

            var result = AssetDatabase.RenameAsset(activeScene.path, newSpaceName);

            await Task.Run(() => { Task.Yield(); } );

            if (!string.IsNullOrEmpty(result))
            {
                return (false, result, null);
            }

            return (true, $"Space name {activeScene.name} is renamed to {newSpaceName}.", null);
        }
    }
}

#endif