using System;
using System.Collections.Generic;

namespace T2G.Assistant
{
    public static class ActionCatalog
    {
        // The action set (lowercase, stable identifiers).
        public static readonly HashSet<string> AllowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            //Project instructions
            "create_project",
            "init_project",
            "open_project",

            //Connection
            "connect",
            "disconnect",

            //space instructions
            "create_space",
            "goto_space",
            "save_space",
            
            //Object instructions
            "create_object",
            "set_object_position",
            "set_object_rotation",
            "set_object_scale",
            "destroy_object",
            "select_object",
            "set_property_value",
            "attach_object_to",
            "detach_object_from",
            
            //component instructions
            "add_component",
            "remove_component",
        };

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