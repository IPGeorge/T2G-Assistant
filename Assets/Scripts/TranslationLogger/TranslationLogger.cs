using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace T2G.Assistant
{
    [Serializable]
    public class TranslationRecord
    {
        public string timestampUtc;     // ISO 8601
        public string prompt;
        public bool success;
        public InstructionList instructionList;
    }

    /// <summary>
    /// Append-only JSONL logger for prompt->instruction pairs.
    /// </summary>
    public static class TranslationLogger
    {
        /// <summary>
        /// If false, raw model output will not be logged (privacy / disk usage).
        /// </summary>
        public static bool LogRawOutputs = true;

        /// <summary>
        /// If true, writes to Unity console when logging fails (does not throw).
        /// </summary>
        public static bool Verbose = true;

        private static readonly object _fileLock = new object();

        public static string LogFolder =>
            Path.Combine(Application.persistentDataPath, "TranslationLogs");

        public static string GetDailyLogFilePath()
        {
            Directory.CreateDirectory(LogFolder);
            string date = DateTime.UtcNow.ToString("yyyyMMdd");
            return Path.Combine(LogFolder, $"translation_log_{date}.jsonl");
        }

        public static void Append(TranslationRecord record)
        {
            if (record == null) return;

            try
            {
                record.timestampUtc ??= DateTime.UtcNow.ToString("o");

                string line = JsonConvert.SerializeObject(record, Formatting.None);

                lock (_fileLock)
                {
                    File.AppendAllText(GetDailyLogFilePath(), line + "\n");
                }
            }
            catch (Exception e)
            {
                if (Verbose)
                    Debug.LogWarning($"[T2G] TranslationLogger failed: {e.Message}");
            }
        }
    }
}
