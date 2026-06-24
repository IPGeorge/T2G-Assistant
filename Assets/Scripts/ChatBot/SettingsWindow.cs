using UnityEngine;
using System;

namespace T2G.Assistant
{
    public class SettingsWindow : MonoBehaviour
    {
        public static SettingsWindow Instance { get; private set; } = null;

        [Header("References")]
        [SerializeField] private ChatBotUI chatBotUI;

        private float windowWidth = 800f;
        private float windowHeight = 400f;

        [Header("Colors")]
        [SerializeField] private Color windowBackgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
        [SerializeField] private Color sectionHeaderColor = new Color(0.2f, 0.2f, 0.3f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.3f, 0.5f, 0.8f, 1f);

        // Window state
        private bool isWindowVisible = false;
        public bool IsWindowVisible => isWindowVisible;

        private Vector2 scrollPosition = Vector2.zero;
        private Settings editingSettings = null;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            editingSettings = ScriptableObject.CreateInstance<Settings>();
            Utils.CopySettings(Assistant.Instance.Settings, editingSettings);
        }

        private void OnGUI()
        {
            if (!isWindowVisible) return;

            // Set GUI skin
            GUI.skin.label.fontSize = 24;
            GUI.skin.textField.fontSize = 24;
            GUI.skin.button.fontSize = 24;
            GUI.skin.box.fontSize = 24;
            GUI.skin.toggle.fontSize = 24;

            // Draw the settings window
            DrawSettingsWindow();
        }

        public void ShowWindow()
        {
            isWindowVisible = true;
            Utils.CopySettings(Assistant.Instance.Settings, editingSettings);
        }

        public void HideWindow()
        {
            isWindowVisible = false;
        }

        private void DrawSettingsWindow()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float windowX = (screenWidth - windowWidth) / 2;
            windowHeight = screenHeight - 100;
            float windowY = (screenHeight - windowHeight) / 2;

            // Window background
            GUI.backgroundColor = windowBackgroundColor;
            GUI.Box(new Rect(windowX, windowY, windowWidth, windowHeight), "");
            GUI.backgroundColor = Color.white;

            // Window title
            DrawSectionHeader(new Rect(windowX, windowY, windowWidth, 50), "ChatBot Settings", true);

            // Close button
            if (GUI.Button(new Rect(windowX + windowWidth - 46, windowY + 7, 36, 36), "X"))
            {
                HideWindow();
                return;
            }

            // Content area
            float contentY = windowY + 60;
            float contentHeight = windowHeight - 130;

            // Scroll view for settings
            Rect viewport = new Rect(windowX + 10, contentY, windowWidth - 20, contentHeight);
            Rect content = new Rect(0, 0, windowWidth - 40, CalculateContentHeight());

            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, content);

            float yPos = 0;

            // Basic Settings Section
            yPos = DrawBasicSettingsSection(yPos, content.width);

            // Color Settings Section
            yPos = DrawColorSettingsSection(yPos, content.width);

            // Auto-save Settings Section
            yPos = DrawAutoSaveSection(yPos, content.width);

            yPos = DrawPathsSection(yPos, content.width);

            GUI.EndScrollView();

            // Action buttons
            DrawActionButtons(windowX, windowY + windowHeight - 50);
        }

        private float CalculateContentHeight()
        {
            float height = 0;

            // Basic settings: 4 fields
            height += 48 + (4 * 42);

            // Color settings: header + 2 colors
            height += 48 + (2 * 100);

            // Auto-save: header + toggle + slider
            height += 48 + 42 + 42;

            // Add response field
            height += 42;

            return height + 60; // Extra padding
        }

        private float DrawBasicSettingsSection(float yPos, float width)
        {
            if (editingSettings != null)
            {
                DrawSectionHeader(new Rect(0, yPos, width, 36), "Basic Settings");
                yPos += 42;

                // Bot Name
                yPos = DrawLabeledTextField("Bot Name:", editingSettings.botName,
                                           value => editingSettings.botName = value,
                                           yPos, width);
                // User Name
                yPos = DrawLabeledTextField("User Name:", editingSettings.userName,
                                           value => editingSettings.userName = value,
                                           yPos, width);
            }
            return yPos;
        }

        private float DrawColorSettingsSection(float yPos, float width)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 36), "Color Settings");
            yPos += 42;

            // User Bubble Color
            GUI.Label(new Rect(0, yPos, 150, 30), "User Message Color:");
            editingSettings.userMessageColor = DrawColorPicker(new Rect(160, yPos, 200, 30),
                                                              editingSettings.userMessageColor);
            yPos += 36;
            DrawBoxWithBackgroundColor(new Rect(370, yPos - 36, 50, 30), "", editingSettings.userMessageColor);

            yPos += 60;
            // Bot Bubble Color
            GUI.Label(new Rect(0, yPos, 150, 30), "Bot Message Color:");
            editingSettings.botMessageColor = DrawColorPicker(new Rect(160, yPos, 200, 30),
                                                             editingSettings.botMessageColor);
            yPos += 36;
            DrawBoxWithBackgroundColor(new Rect(370, yPos - 36, 50, 30), "", editingSettings.botMessageColor);

            return yPos + 60;
        }

        private void DrawBoxWithBackgroundColor(Rect rect, string text, Color bgColor)
        {
            var saved = GUI.skin.box.normal.background;
            var savedBGColor = GUI.backgroundColor;
            GUI.skin.box.normal.background = Texture2D.whiteTexture;
            GUI.backgroundColor = bgColor;
            GUI.Box(rect, text);
            GUI.backgroundColor = Color.white;
            GUI.skin.box.normal.background = saved;
            GUI.backgroundColor = savedBGColor;
        }

        private float DrawAutoSaveSection(float yPos, float width)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 36), "Auto-save Settings");
            yPos += 42;

            // Toggle
            editingSettings.autoSaveHistory = GUI.Toggle(new Rect(0, yPos, 200, 30),
                                                         editingSettings.autoSaveHistory,
                                                         "Enable Auto-save");
            yPos += 36;

            // Interval slider (only if enabled)
            if (editingSettings.autoSaveHistory)
            {
                GUI.Label(new Rect(0, yPos, 100, 30), "Interval:");
                editingSettings.autoSaveInterval = GUI.HorizontalSlider(new Rect(110, yPos, 200, 30),
                                                                        editingSettings.autoSaveInterval,
                                                                        60f, 600f);
                GUI.Label(new Rect(320, yPos, 100, 30), $"{editingSettings.autoSaveInterval:F0} seconds");
                yPos += 36;
            }

            return yPos;
        }

        private float DrawPathsSection(float yPos, float width)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 36), "Paths");
            yPos += 42;

            DrawLabeledTextField("Unity Editor Path:", editingSettings.UnityEditorPath,
                value => editingSettings.UnityEditorPath = value, yPos, width);
            yPos += 36;

            DrawLabeledTextField("T2G Plugin Path:", editingSettings.T2G_UnityPluginPath,
                value => editingSettings.T2G_UnityPluginPath = value, yPos, width);
            yPos += 36;

            DrawLabeledTextField("Asset Library Path:", editingSettings.AssetLibraryRootPath,
                value => editingSettings.AssetLibraryRootPath = value, yPos, width);
            yPos += 36;

            return yPos;
        }

        private void DrawActionButtons(float windowX, float buttonY)
        {
            float buttonWidth = 140f;

            GUI.backgroundColor = buttonColor;

            // Apply button
            if (GUI.Button(new Rect(windowX + 30, buttonY, buttonWidth, 50), "Apply"))
            {
                ApplyChanges();
                HideWindow();
            }

            // Cancel button
            if (GUI.Button(new Rect(windowX + 600, buttonY, buttonWidth, 50), "Cancel"))
            {
                HideWindow();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawSectionHeader(Rect rect, string title, bool isWindowHeader = false)
        {
            GUI.backgroundColor = isWindowHeader ? sectionHeaderColor : new Color(0.2f, 0.2f, 0.3f, 0.8f);
            GUI.Box(rect, title);
            GUI.backgroundColor = Color.white;
        }

        private float DrawLabeledTextField(string label, string currentValue, Action<string> setter, float yPos, float width)
        {
            GUI.Label(new Rect(0, yPos, 150, 30), label);
            string newValue = GUI.TextField(new Rect(160, yPos, width - 160, 30), currentValue);
            setter(newValue);
            return yPos + 36;
        }

        private Color DrawColorPicker(Rect rect, Color color)
        {
            // Simple color picker with RGB sliders
            float sliderY = rect.y;

            // R
            GUI.Label(new Rect(rect.x, sliderY, 20, 28), "R:");
            color.r = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 28), color.r, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 28), $"{color.r:F2}");

            sliderY += 30;

            // G
            GUI.Label(new Rect(rect.x, sliderY, 20, 28), "G:");
            color.g = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 28), color.g, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 28), $"{color.g:F2}");

            sliderY += 30;

            // B
            GUI.Label(new Rect(rect.x, sliderY, 20, 28), "B:");
            color.b = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 28), color.b, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 28), $"{color.b:F2}");

            return color;
        }

        private void ApplyChanges()
        {
            chatBotUI?.SaveSettings(editingSettings);
        }
    }
}