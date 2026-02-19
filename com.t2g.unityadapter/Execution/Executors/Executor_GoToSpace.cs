#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace T2G
{
    [Executor("goto_space")]
    public class Executor_GoToSpace : ExecutorBase
    {
        private readonly string k_Assets = "Assets";
        private readonly string k_SpacesDirectory = "Spaces";

        private bool _isSpaceOpened;
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string spaceName = instruction.parameters.GetString("spaceName");
            string spacesPath = Path.Combine(Application.dataPath, k_SpacesDirectory);
            string spaceFile = Path.Combine(spacesPath, spaceName + ".unity");
            string space = Path.Combine(k_Assets, k_SpacesDirectory, spaceName + ".unity");

            if (!Directory.Exists(spacesPath) || string.IsNullOrWhiteSpace(spaceName) || !File.Exists(spaceFile))
            {
                return (false, "Space doesn't exist!", null);
            }

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

            _isSpaceOpened = false;
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                _isSpaceOpened = true;
            };
            EditorSceneManager.OpenScene(space, OpenSceneMode.Single);

            await WaitForSpaceIsOpened();

            return (true, $"Entered {spaceName} space.", null);
        }

        async Awaitable WaitForSpaceIsOpened()
        {
            while (!_isSpaceOpened)
            {
                await Task.Yield();
            }
        }
    }
}

#endif