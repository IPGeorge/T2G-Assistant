using System.IO;
using UnityEngine;

namespace T2G.Assistant
{
    public class Utils
    {
        public static void CopySettings(Settings source, Settings destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.botName = source.botName;
            destination.userName = source.userName;
            destination.userMessageColor = source.userMessageColor;
            destination.botMessageColor = source.botMessageColor;
            destination.autoSaveHistory = source.autoSaveHistory;
            destination.autoSaveInterval = source.autoSaveInterval;
            destination.UnityEditorPath = source.UnityEditorPath;
            destination.T2G_UnityPluginPath = source.T2G_UnityPluginPath;
            destination.AssetLibraryRootPath = source.AssetLibraryRootPath;
        }

        public static string GetParamFromArguments((string name, string value)[] arguments, string paramName)
        {
            foreach (var argument in arguments)
            {
                if (string.Compare(argument.name, paramName) == 0)
                {
                    return argument.value;
                }
            }
            return null;
        }

        public static bool IsValidPath(string path)
        {
            try
            {
                Path.GetFullPath(path);
                return path.IndexOfAny(Path.GetInvalidPathChars()) < 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidFileName(string fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName) &&
                   fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   !fileName.EndsWith(".") &&
                   !fileName.EndsWith(" ");
        }

    }
}