using System;
using UnityEngine;

namespace T2G.Assistant
{
    [CreateAssetMenu(fileName = "ChatBotSettings", menuName = "ChatBot/Settings")]
    public class Settings : ScriptableObject
    {
        [Header("Bot Configuration")]
        public string botName = "Assistant";
        public string userName = "You";

        [Header("UI Colors")]
        public Color userMessageColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public Color botMessageColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        [Header("History Settings")]
        public bool autoSaveHistory = true;
        public float autoSaveInterval = 60f; // 1 minute

        [Header("Environment Settings")]
        public string UnityEditorPath;
        public string T2G_UnityPluginPath;
        public string AssetLibraryRootPath;

        public SettingsLite CloneToSettingsLite()
        {
            SettingsLite settingsLite = new SettingsLite();
            settingsLite.AssetLibraryRootPath = AssetLibraryRootPath;
            return settingsLite;
        }
    }


    [Serializable]
    public class T2G_Settings
    {
        public string botName;
        public string userName;
        public float[] userMessageColor = new float[4];
        public float[] botMessageColor = new float[4];
        public bool autoSaveHistory;
        public float autoSaveInterval;
        public string UnityEditorPath;
        public string T2G_UnityPluginPath;
        public string AssetLibraryRootPath;

        public void CopyFrom(Settings settings)
        {
            botName = settings.botName;
            userName = settings.userName;
            userMessageColor[0] = settings.userMessageColor.r;
            userMessageColor[1] = settings.userMessageColor.g;
            userMessageColor[2] = settings.userMessageColor.b;
            userMessageColor[3] = settings.userMessageColor.a;
            botMessageColor[0] = settings.botMessageColor.r;
            botMessageColor[1] = settings.botMessageColor.g;
            botMessageColor[2] = settings.botMessageColor.b;
            botMessageColor[3] = settings.botMessageColor.a;
            autoSaveHistory = settings.autoSaveHistory;
            autoSaveInterval = settings.autoSaveInterval;
            UnityEditorPath = settings.UnityEditorPath;
            T2G_UnityPluginPath = settings.T2G_UnityPluginPath;
            AssetLibraryRootPath = settings.AssetLibraryRootPath;
        }

        public void CopyTo(Settings settings)
        {
            settings.botName = botName;
            settings.userName = userName;
            settings.userMessageColor = new Color(userMessageColor[0], userMessageColor[1], userMessageColor[2], userMessageColor[3]);
            settings.botMessageColor = new Color(botMessageColor[0], botMessageColor[1], botMessageColor[2], botMessageColor[3]);
            settings.autoSaveHistory = autoSaveHistory;
            settings.autoSaveInterval = autoSaveInterval;
            settings.UnityEditorPath = UnityEditorPath;
            settings.T2G_UnityPluginPath = T2G_UnityPluginPath;
            settings.AssetLibraryRootPath = AssetLibraryRootPath;
        }
    }
}