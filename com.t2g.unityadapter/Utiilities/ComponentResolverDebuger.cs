#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEngine;

namespace T2G
{
    public class ComponentResolverDebugger : MonoBehaviour
    {
        [ContextMenu("Log All Component Types")]
        void LogAllComponentTypes()
        {
            var allTypes = ComponentResolver.GetAllComponentTypes();
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Found {allTypes.Count} unique component names:");

            foreach (var kvp in allTypes.OrderBy(k => k.Key))
            {
                if (kvp.Value.Count == 1)
                {
                    sb.AppendLine($"  {kvp.Key} -> {kvp.Value[0].Name}");
                }
                else
                {
                    sb.AppendLine($"  {kvp.Key} -> (multiple: {string.Join(", ", kvp.Value.Select(t => t.Name))})");
                }
            }

            Debug.Log(sb.ToString());
        }

        [ContextMenu("Re-Initialize All Component Types")]
        void ReinitializeAllComponentTypes()
        {
            ComponentResolver.Reset();
            LogAllComponentTypes();

        }


        [ContextMenu("Test Common Names")]
        void TestCommonNames()
        {
            string[] testNames = {
            "rigidbody", "rb", "body",
            "box collider", "box", "boxcol",
            "transform", "tr", "t",
            "camera", "cam",
            "light", "l",
            "audio source", "audio",
            "animator", "anim",
            "canvas", "ui",
            "button", "btn",
            "text", "txt",
            "image", "img",
            "slider",
            "toggle", "tog",
            "input field", "input",
            "dropdown", "drop",
            "nav mesh agent", "agent", "navagent",
            "rigidbody2d", "rb2d",
            "box collider2d",
            "sphere collider2d",
            "particle system", "particle", "ps"
        };

            foreach (string name in testNames)
            {
                var type = ComponentResolver.GetComponentType(name);
                if (type != null)
                {
                    Debug.Log($"✓ '{name}'");
                }
                else
                {
                    Debug.Log($"✗ '{name}' -> Not found");
                }
            }
        }
    }
#endif
}