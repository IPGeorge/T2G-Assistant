using System;
using UnityEditor;
using UnityEngine;

namespace T2G
{
    public class Utils
    {
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