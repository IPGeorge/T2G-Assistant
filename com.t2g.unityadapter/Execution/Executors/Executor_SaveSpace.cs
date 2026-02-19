#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace T2G
{
    [Executor("save_space")]
    public class Executor_SaveSpace : ExecutorBase
    {
        private readonly string k_Assets = "Assets";
        private readonly string k_SpacesDirectory = "Spaces";

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.name))
            {
                string untitledSpace = Path.Combine(k_Assets, k_SpacesDirectory, "UntitledSpace.unity");
                EditorSceneManager.SaveScene(activeScene, untitledSpace);
            }
            else
            {
                EditorSceneManager.SaveScene(activeScene);
            }

            return (true, $"Space was saved!", null);
        }
    }
}

#endif