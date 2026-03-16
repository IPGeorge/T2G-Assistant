
namespace T2G
{
    public static class Actions
    {

        #region Project 
        public const string create_project = "create_project";
        public const string init_project = "init_project";
        public const string open_project = "open_project";
        #endregion Project

        #region Connection
        public const string connect = "connect";
        public const string disconnect = "disconnect";
        #endregion Connection

        #region Misc
        public const string clear = "clear";
        #endregion Misc

        #region Space
        public const string create_space = "create_space";
        public const string goto_space = "goto_space";
        public const string save_space = "save_space";
        #endregion Space

        #region Object
        public const string create_object = "create_object";
        public const string select_object = "select_object";
        public const string delete_object = "delete_object";
        public const string place_on_ground = "place_on_ground";
        public const string create_from = "create_from";
        public const string attach_to = "attach_to";
        public const string detach = "detach";
        #endregion Object

        #region Component
        public const string add_component = "add_component";
        public const string remove_component = "remove_component";
        #endregion Component
    }

    public static class Components
    {

    }

}
