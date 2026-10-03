
using System.Collections.Generic;

namespace T2G
{
    public static class Actions
    {
        public const string no_action = "no_action";

        #region Project 
        public const string create_project = "create_project";
        public const string create_from = "create_from";
        public const string init_project = "init_project";
        public const string open_project = "open_project";
        #endregion Project

        #region Connection
        public const string connect = "connect";
        public const string disconnect = "disconnect";
        #endregion Connection

        #region Misc
        public const string clear = "clear";
        public const string import_assets = "import_assets";
        #endregion Misc

        #region Space
        public const string create_space = "create_space";
        public const string goto_space = "goto_space";
        public const string save_space = "save_space";
        public const string rename_space = "rename_space";
        #endregion Space

        #region Object
        public const string create_object = "create_object";
        public const string select_object = "select_object";
        public const string delete_object = "delete_object";
        public const string place_on = "place_on";
        public const string attach_to = "attach_to";
        public const string detach_from = "detach_from";
        public const string set_property = "set_property";
        public const string set_relationship = "set_relationship";
        #endregion Object

        #region Component
        public const string add_component = "add_component";
        public const string remove_component = "remove_component";
        public const string call_method = "call_method";
        #endregion Component


        public class ActionMeta
        {
            public InstructionType instructionType;
            public InstructionState instructionState;
            public string[] parameters = null;
        }

        static Dictionary<string, ActionMeta> s_actionsMap = null;
        public static Dictionary<string, ActionMeta> ActionMap
        {
            get
            {
                if(s_actionsMap == null)
                {
                    BuildActionsMap();
                }
                return s_actionsMap;
            }
        }

        static void BuildActionsMap()
        {
            s_actionsMap = new Dictionary<string, ActionMeta>();
            s_actionsMap[Actions.no_action] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = null };
            s_actionsMap[Actions.create_project] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = new string[]{ "Path", "ProjectName" } };
            s_actionsMap[Actions.create_from] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = new string[] { "GameDesc", "Spaces" } };
            s_actionsMap[Actions.init_project] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = new string[] { "Path", "ProjectName" } };
            s_actionsMap[Actions.open_project] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = new string[] { "Path", "ProjectName" } };
            s_actionsMap[Actions.connect] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = null };
            s_actionsMap[Actions.disconnect] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = null };
            s_actionsMap[Actions.clear] = new ActionMeta() { instructionType = InstructionType.Local, instructionState = InstructionState.Resolved, parameters = null };
            s_actionsMap[Actions.import_assets] = new ActionMeta() { instructionType = InstructionType.Sequence, instructionState = InstructionState.Resolved, parameters = new string[] { "Path", "ProjectName" } };
            s_actionsMap[Actions.create_space] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "SpaceName" } };
            s_actionsMap[Actions.goto_space] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "SpaceName" } };
            s_actionsMap[Actions.save_space] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = null };
            s_actionsMap[Actions.rename_space] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "SpaceName" } };
            s_actionsMap[Actions.create_object] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Raw, parameters = new string[] { "Position", "Name" } };
            s_actionsMap[Actions.select_object] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Name" } };
            s_actionsMap[Actions.delete_object] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Name" } };
            s_actionsMap[Actions.place_on] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Name" } };
            s_actionsMap[Actions.attach_to] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Source", "Target" } };
            s_actionsMap[Actions.detach_from] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Name" } };
            s_actionsMap[Actions.set_property] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "ObjName", "Property", "Value" } };
            s_actionsMap[Actions.set_relationship] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "Source", "Target", "Type", "Slot" } };
            s_actionsMap[Actions.add_component] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "ObjName", "Type" } };
            s_actionsMap[Actions.remove_component] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "ObjName", "ComponentType" } };
            s_actionsMap[Actions.call_method] = new ActionMeta() { instructionType = InstructionType.Elemental, instructionState = InstructionState.Resolved, parameters = new string[] { "ObjName", "Method" } };
        }
    }
}