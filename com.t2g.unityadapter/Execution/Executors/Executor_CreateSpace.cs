#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace T2G
{
    [Executor("create_space")]
    public class Executor_CreateSpace : ExecutorBase
    {
        private readonly string k_Assets = "Assets";
        private readonly string k_SpacesDirectory = "Spaces";
        private readonly string k_DefaultSpaceName = "MetaSpace";

        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string spacesPath = Path.Combine(Application.dataPath, k_SpacesDirectory);
            if (!Directory.Exists(spacesPath))
            {
                Directory.CreateDirectory(spacesPath);
            }

            //Save active scene
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

            string spaceName = instruction.parameters.GetString("spaceName");
            if (string.IsNullOrEmpty(spaceName))
            {
                spaceName = k_DefaultSpaceName;
            }
            string spaceNameScene = spaceName + ".unity";

            string spaceFile = Path.Combine(spacesPath, spaceNameScene);
            string space = Path.Combine(k_Assets, k_SpacesDirectory, spaceNameScene);
            if (File.Exists(spaceFile))
            {
                bool isOpened = false;
                EditorSceneManager.sceneOpened += (scene, mode) =>
                {
                    isOpened = true;
                };
                EditorSceneManager.OpenScene(space, OpenSceneMode.Single);
                await Task.Run(() => { while (!isOpened) { Task.Yield(); } });
            }
            else
            {
                bool isCreated = false;
                EditorSceneManager.newSceneCreated += (scene, setup, mode) =>
                {
                    EditorSceneManager.SaveScene(scene, space);

                    //Add Scene To Build Settings Scene List
                    var scenes = EditorBuildSettings.scenes;
                    int len = scenes.Length;
                    EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[len + 1];
                    System.Array.Copy(scenes, newScenes, len);
                    newScenes[len] = new EditorBuildSettingsScene(space, true);
                    EditorBuildSettings.scenes = newScenes;

                    isCreated = true;
                };
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                await Task.Run(() => { while (!isCreated) { Task.Yield(); } });
            }
            return (true, $"Entered {spaceName} space.", null);
        }
    }
}

#endif