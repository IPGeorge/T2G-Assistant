
namespace T2G
{
    public static class Actions
    {
        public const string no_action = "no_action";
        public const string question = "question";

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
        public const string place_on_ground = "place_on_ground";
        public const string attach_to = "attach_to";
        public const string detach_from = "detach_from";
        public const string set_property = "set_property";
        #endregion Object

        #region Component
        public const string add_script = "add_script";
        public const string remove_script = "remove_script";
        public const string update_script = "update_script";
        public const string call_method = "call_method";
        #endregion Component
    }

}
