using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class PathValidator
{
    /// <summary>
    /// Checks if a string is a valid file path (syntactically, not necessarily existing)
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <param name="checkExistence">If true, also checks if the file exists</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidFilePath(string path, bool checkExistence = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Trim whitespace
            path = path.Trim();

            // Check for invalid characters
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            // Check for wildcards (not allowed in specific file paths)
            if (path.Contains('*') || path.Contains('?'))
                return false;

            // Check for common invalid patterns
            if (path.StartsWith(" ") || path.EndsWith(" ") || path.EndsWith("."))
                return false;

            // Check if it's a valid root (e.g., C:\, \\server\share)
            if (!IsValidRoot(path) && !Path.IsPathRooted(path))
            {
                // Relative paths are allowed, but check they don't go beyond root
                if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => part == ".." && path.StartsWith("..")))
                {
                    // Too many parent directory references
                    if (CountParentReferences(path) > 1)
                        return false;
                }
            }

            // Check file name if present
            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;

                // Check for reserved names in Windows
                if (!IsValidFileName(fileName))
                    return false;
            }

            // Check path length (Windows limitation)
            if (path.Length > 260 && !path.StartsWith(@"\\?\"))
                return false;

            // Optional: Check if file exists
            if (checkExistence)
            {
                return File.Exists(path);
            }

            // Try to get full path - this will throw if path is invalid
            string fullPath = Path.GetFullPath(path);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Quick validation without full path resolution (faster)
    /// </summary>
    public static bool IsValidFilePathQuick(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for invalid characters
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return false;

        // Check for wildcards
        if (path.Contains('*') || path.Contains('?'))
            return false;

        // Check file name if present
        string fileName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (!IsValidFileName(fileName))
                return false;
        }

        // Basic format check for Windows paths
        if (path.Contains(':'))
        {
            // Check if drive letter is valid (A-Z)
            if (!Regex.IsMatch(path, @"^[A-Za-z]:\\"))
                return false;
        }

        return true;
    }

    private static bool IsValidRoot(string path)
    {
        // Check for Windows drive roots (C:\, D:\, etc.)
        if (Regex.IsMatch(path, @"^[A-Za-z]:\\$"))
            return true;

        // Check for UNC paths (\\server\share)
        if (path.StartsWith(@"\\") && path.Length > 2)
        {
            string[] parts = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2; // At least server and share
        }

        return false;
    }

    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Windows reserved names
        string[] reservedNames = {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();

        // Check for reserved names
        if (reservedNames.Contains(nameWithoutExt))
            return false;

        // Check for reserved names with extensions (e.g., CON.txt is also invalid)
        if (reservedNames.Contains(fileName.ToUpperInvariant()))
            return false;

        // Check for trailing spaces or dots
        if (fileName.EndsWith(" ") || fileName.EndsWith("."))
            return false;

        return true;
    }

    private static int CountParentReferences(string path)
    {
        int count = 0;
        string[] parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (string part in parts)
        {
            if (part == "..")
                count++;
        }

        return count;
    }
}