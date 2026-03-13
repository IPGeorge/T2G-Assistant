using System;
using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{
    public static class ActionCatalog
    {
        static HashSet<string> m_allowedActions = null; // The action set (lowercase, stable identifiers).

        public static void InitAllowedActions()
        {
            m_allowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FieldInfo[] fields = typeof(T2G.Actions).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            foreach(var field in fields)
            {
                // Check if it's a literal (const) and of type string
                if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                {
                    string value = field.GetValue(null) as string;
                    if (value != null)
                    {
                        m_allowedActions.Add(value);
                    }
                }
            }
        }

        public static bool IsValidAction(string action)
        {
            if(m_allowedActions == null)
            {
                InitAllowedActions();
            }

            return m_allowedActions.Contains(action);
        }

        // A compact spec to help the model choose correct params.
        public static string BuildActionSpecForSystemPrompt()
        {
            // Keep this short-ish; long prompts can hurt smaller local models.
            // You can expand param descriptions as your schema stabilizes.
            return
@"Allowed actions (choose only from this list):
Project:
- create_project(state: Local, params: { path, projectName })
- initi_project(state: Local, params: { path, projectName })
- open_project(state: Local, params: { path, projectName })

Connection:
- connect(state: Local)
- disconnect(state: Local)

Space (scene/level):
- create_space(state: Resolved, params: { spaceName })
- goto_space(state: Resolved, params: { spaceName })
- save_space(state: Resolved, params: { spaceName })

Objects:
- create_object(state: Raw, params: { name, primitiveType?, position?, rotation?, scale?, parent? })
- set_object_position(state: Resolved, params: { name, position })
- set_object_rotation(state: Resolved, params: { name, rotation })
- set_object_scale(state: Resolved, params: { name, scale })
- destroy_object(state: Resolved, params: { name })
- select_object(state: Resolved, params: { name })
- set_property_value(state: Resolved, params: { name, propertyPath, value })
- attach_object_to(state: Resolved, params: { childName, parentName })
- detach_object_from(state: Resolved, params: { childName })

Components:
- add_component(state: Raw, params: { name, componentType, properties? })
- remove_component(state: Resolved, params: { name, componentType })";
        }
    }
}