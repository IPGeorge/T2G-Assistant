using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System.Reflection;
using System.IO;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace T2G
{
    public class Utils
    {
#if UNITY_EDITOR
        public static GameObject FindObjectByName(string objName, bool onlyRootObjects = false)
        {
            GameObject gameObject = null;
            //TODO: need to optimize with a performative search. 
            if (onlyRootObjects)
            {
                gameObject = SceneManager.GetActiveScene()
                    .GetRootGameObjects()
                    .Where(obj => obj.name == objName)
                    .FirstOrDefault();
            }
            else
            {
                GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (GameObject root in rootObjects)
                {
                    if (root.name == objName)
                    {
                        gameObject = root;
                        break;
                    }

                    Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in allChildren)
                    {
                        if (child.name == objName)
                        {
                            gameObject = child.gameObject;
                            break;
                        }
                    }
                }
            }

            return gameObject;
        }

        public static async Awaitable WaitForUnityIdle()
        {
            // Wait for Unity script compilation to finish
            if (EditorApplication.isCompiling)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                void OnCompilationFinished(object obj)
                {
                    CompilationPipeline.compilationFinished -= OnCompilationFinished;
                    tcs.TrySetResult(true);
                }
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                await tcs.Task;
            }

            // Wait for asset database to finish updating
            while (EditorApplication.isUpdating)
            {
                await Task.Yield();
            }
        }
#endif

        /// <summary>
        /// Checks if a script is a valid MonoBehaviour and returns the class name
        /// </summary>
        /// <param name="scriptContent">The script content or file path</param>
        /// <returns>The class name if valid, null otherwise</returns>
        public static string GetMonoBehaviourClassName(string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
                return null;

            // Check for MonoBehaviour inheritance
            // Pattern: class ClassName : MonoBehaviour
            Match match = Regex.Match(scriptContent, @"class\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*:\s*MonoBehaviour", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value; // Return the class name
            }

            return null;
        }

        public static bool IsValidComponentName(string componentName)
        {
            bool isValid = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Any(t => t.Name == componentName && t.IsSubclassOf(typeof(MonoBehaviour)));
            return isValid;
        }

        public static bool IsPrimitiveDesc(string desc, out PrimitiveType? primitiveType)
        {
            if (string.IsNullOrEmpty(desc))
            {
                primitiveType = null;
                return false;
            }

            foreach (PrimitiveType enumType in Enum.GetValues(typeof(PrimitiveType)))
            {
                string typeName = enumType.ToString();
                if (desc.Length >= typeName.Length &&
                    string.Compare(typeName, desc.Substring(0, typeName.Length), true) == 0)
                {
                    primitiveType = enumType;
                    return true;
                }
            }
            primitiveType = null;
            return false;
        }

        public static bool IsCameraDesc(string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                return false;
            }
            return (desc.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        
        public static bool IsOrthographicsCameraDesc(string desc)
        {

            return (desc.IndexOf("parallel", StringComparison.OrdinalIgnoreCase) >= 0 ||
               desc.IndexOf("orthographic", StringComparison.OrdinalIgnoreCase) >= 0 ||
               desc.IndexOf("orthogonal", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsLightDesc(string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                return false;
            }
            return (desc.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static LightType GetLightTypeFromDesc(string desc)
        {
            if (desc.IndexOf("spot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LightType.Spot;
            }
            else if (desc.IndexOf("point", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LightType.Point;
            }
            else // "directional" by default
            {
                return LightType.Directional;
            }
        }

        public static bool IsObjectDesc(string desc)
        {
            if(string.IsNullOrEmpty(desc))
            {
                return false;
            }
            return (string.Compare(desc.Trim(), "object", true) == 0);
        }

        public static void PlaceInFrontOfCamera(GameObject gameObject,
            float distance = 5.0f, 
            bool alignToGround = false, float groundHeight = 0.0f)
        {
            var camera = GetEditorCamera();

            // Calculate base position
            gameObject.transform.position = camera.transform.position + (camera.transform.forward * distance);

            // Adjust for ground if needed
            if (alignToGround)
            {
                PlaceOnGround(gameObject, groundHeight);
            }
        }

        public static void PlaceOnGround(GameObject obj, float groundHeight = 0.0f)
        {
            float objectHeight = GetObjectHeight(obj);
            float bottomOffset = GetBottomOffset(obj);

            RaycastHit hit;
            Vector3 position = obj.transform.position;

            if (Physics.Raycast(position, Vector3.down, out hit, Mathf.Infinity))
            {
                // Formula: pivot position = ground point + (bottomOffset * up direction)
                position.y = hit.point.y + bottomOffset;
            }
            else
            {
                position.y = groundHeight + bottomOffset;
            }
            obj.transform.position = position;
        }


        public static float GetObjectHeight(GameObject obj)
        {
            Bounds bounds = GetObjectBounds(obj);
            return bounds.size.y;
        }

        public static float GetBottomOffset(GameObject obj)
        {
            Bounds bounds = GetObjectBounds(obj);

            // Calculate how far the pivot is from the bottom of the bounds
            // Positive value means pivot is above bottom, negative means pivot is below bottom
            return bounds.center.y - bounds.min.y;
        }

        public static Bounds GetObjectBounds(GameObject obj)
        {
            // Try to get renderer bounds first
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            // Try collider bounds
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            // If no renderer or collider, use transform position with default size
            return new Bounds(obj.transform.position, Vector3.one);
        }

        public static Camera GetEditorCamera()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            return sceneView?.camera;
        }


        static MethodInfo _repaintAllMethod = null;
        public static void UpdateEditorViews()
        {

            if (_repaintAllMethod == null)
            {
                var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.View");
                if (viewType != null)
                {
                    _repaintAllMethod = viewType.GetMethod("RepaintAll", BindingFlags.Static | BindingFlags.NonPublic);
                }
            }

            EditorApplication.QueuePlayerLoopUpdate();

            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                sceneView.Repaint();
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }

            EditorApplication.DirtyHierarchyWindowSorting();

            _repaintAllMethod?.Invoke(null, null);

            //GUIUtility.ExitGUI();

            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView != null && sceneView.camera != null)
                {
                    sceneView.camera.Render();
                }
            }
        }

        public static string GetProjectName()
        {
            string projectPath = GetProjectPath(false);
            string projectName = new DirectoryInfo(projectPath).Name;
            return projectName;
        }

        public static string GetProjectPath(bool excludeProjectName = true)
        {
            try
            {
                string path = Application.dataPath;

                // Remove the "/Assets" part
                if (path.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase) || path.EndsWith("\\Assets", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - 7);
                }

                if(excludeProjectName)
                {
                    path = Directory.GetParent(path).FullName;
                }

                return path;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to get project path: {e.Message}");
                return null;
            }
        }

        public static Vector3? ParseVector3(string vector3Str)
        {
            try
            {
                // Remove parentheses and parse
                vector3Str = vector3Str.Trim('(', ')', ' ');
                var parts = vector3Str.Split(',');
                if (parts.Length == 3)
                {
                    float x = float.Parse(parts[0].Trim());
                    float y = float.Parse(parts[1].Trim());
                    float z = float.Parse(parts[2].Trim());
                    return new Vector3(x, y, z);
                }
            }
            catch { }
            return null;
        }

        public static Vector2? ParseVector2(string vector2Str)
        {
            try
            {
                // Remove parentheses and parse
                vector2Str = vector2Str.Trim('(', ')', ' ');
                var parts = vector2Str.Split(',');
                if (parts.Length == 2)
                {
                    float x = float.Parse(parts[0].Trim());
                    float y = float.Parse(parts[1].Trim());
                    return new Vector2(x, y);
                }
            }
            catch { }
            return null;
        }

        public static Quaternion? ParseQuaternion(string quaternionStr)
        {
            try
            {
                // Remove parentheses and parse
                quaternionStr = quaternionStr.Trim('(', ')', ' ');
                var parts = quaternionStr.Split(',');
                if (parts.Length == 4)
                {
                    float x = float.Parse(parts[0].Trim());
                    float y = float.Parse(parts[1].Trim());
                    float z = float.Parse(parts[2].Trim());
                    float w = float.Parse(parts[3].Trim());
                    return new Quaternion(x, y, z, w);
                }
            }
            catch { }
            return null;
        }

        public static Canvas CreateCanvasSystem()
        {
            Canvas canvas = GameObject.FindFirstObjectByType<Canvas>();
            GameObject canvasGO;
            if (canvas == null)
            {
                canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvasGO = canvas.gameObject;
            }

            // Check if an EventSystem already exists to prevent duplicates
            if (GameObject.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }

        public static int CollectAllAssets(Instruction[] instructions, ref List<string> assetList)
        {
            if(instructions == null || instructions.Length <= 0 || assetList == null)
            {
                return 0;
            }

            int cnt = 0;

            foreach (var instruction in instructions)
            {
                if(instruction == null || instruction.assets == null || instruction.assets.Count <= 0)
                {
                    continue;
                }

                foreach (var asset in instruction.assets)
                {
                    assetList.Add(asset);
                    cnt++;
                }

                if (instruction.instructions != null)
                {
                    cnt += CollectAllAssets(instruction.instructions, ref assetList);
                }
            }

            return cnt;
        }
    }
}