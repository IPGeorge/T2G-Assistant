using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
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

        public static bool IsPrimitiveDesc(string desc, out PrimitiveType? primitiveType)
        {
            foreach (PrimitiveType enumType in Enum.GetValues(typeof(PrimitiveType)))
            {
                string typeName = enumType.ToString();
                if (string.Compare(typeName, desc, true) == 0)
                {
                    primitiveType = enumType;
                    return true;
                }
            }
            primitiveType = null;
            return false;
        }

        public static LightType GetLightTypeFromDesc(string desc)
        {
            if(desc.IndexOf("directional", StringComparison.OrdinalIgnoreCase) >=0)
            {
                return LightType.Directional;
            }
            else if (desc.IndexOf("spot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LightType.Spot;
            }
            else 
            {
                return LightType.Point;
            }
        }

        public static bool IsObjectDesc(string desc)
        {
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

        public static void UpdateEditorViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                sceneView.Repaint();
            }
        }
    }
}