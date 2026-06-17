using System;
using System.IO;

public static class ProjectCleaner
{
    public static void CleanProjectBeforeOpen(string projectPath)
    {
        // Always delete Library and Temp folders before opening
        string libraryPath = Path.Combine(projectPath, "Library");
        string tempPath = Path.Combine(projectPath, "Temp");

        if (Directory.Exists(libraryPath))
        {
            try
            {
                Directory.Delete(libraryPath, true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to delete Library folder: {ex.Message}");
            }
        }

        if (Directory.Exists(tempPath))
        {
            try
            {
                Directory.Delete(tempPath, true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to delete Temp folder: {ex.Message}");
            }
        }
    }
}