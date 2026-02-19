using UnityEngine;
using System;

namespace T2G.Assistant
{
    public class SettingsWindow : MonoBehaviour
    {
        public static SettingsWindow Instance { get; private set; } = null;

        [Header("References")]
        [SerializeField] private ChatBotUI chatBotUI;

        private float windowWidth = 600f;
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
            GUI.skin.label.fontSize = 14;
            GUI.skin.textField.fontSize = 14;
            GUI.skin.button.fontSize = 14;
            GUI.skin.box.fontSize = 14;
            GUI.skin.toggle.fontSize = 14;

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
            DrawSectionHeader(new Rect(windowX, windowY, windowWidth, 40), "ChatBot Settings", true);

            // Close button
            if (GUI.Button(new Rect(windowX + windowWidth - 40, windowY + 5, 30, 30), "X"))
            {
                HideWindow();
                return;
            }

            // Content area
            float contentY = windowY + 50;
            float contentHeight = windowHeight - 110;

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

            // Basic settings: 4 fields × 35px
            height += 40 + (4 * 35);

            // Color settings: header + 2 colors
            height += 40 + (2 * 80);

            // Auto-save: header + toggle + slider
            height += 40 + 35 + 35;

            // Add response field
            height += 35;

            return height + 50; // Extra padding
        }

        private float DrawBasicSettingsSection(float yPos, float width)
        {
            if (editingSettings != null)
            {
                DrawSectionHeader(new Rect(0, yPos, width, 30), "Basic Settings");
                yPos += 35;

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
            DrawSectionHeader(new Rect(0, yPos, width, 30), "Color Settings");
            yPos += 35;

            // User Bubble Color
            GUI.Label(new Rect(0, yPos, 150, 25), "User Message Color:");
            editingSettings.userMessageColor = DrawColorPicker(new Rect(160, yPos, 200, 25),
                                                             editingSettings.userMessageColor);
            yPos += 30;
            DrawBoxWithBackgroundColor(new Rect(370, yPos - 30, 50, 25), "", editingSettings.userMessageColor);

            yPos += 50;
            // Bot Bubble Color
            GUI.Label(new Rect(0, yPos, 150, 25), "Bot Message Color:");
            editingSettings.botMessageColor = DrawColorPicker(new Rect(160, yPos, 200, 25),
                                                            editingSettings.botMessageColor);
            yPos += 30;
            DrawBoxWithBackgroundColor(new Rect(370, yPos - 30, 50, 25), "", editingSettings.botMessageColor);

            return yPos + 50;
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
            DrawSectionHeader(new Rect(0, yPos, width, 30), "Auto-save Settings");
            yPos += 35;

            // Toggle
            editingSettings.autoSaveHistory = GUI.Toggle(new Rect(0, yPos, 200, 25),
                                                        editingSettings.autoSaveHistory,
                                                        "Enable Auto-save");
            yPos += 30;

            // Interval slider (only if enabled)
            if (editingSettings.autoSaveHistory)
            {
                GUI.Label(new Rect(0, yPos, 100, 25), "Interval:");
                editingSettings.autoSaveInterval = GUI.HorizontalSlider(new Rect(110, yPos, 200, 25),
                                                                       editingSettings.autoSaveInterval,
                                                                       60f, 600f);
                GUI.Label(new Rect(320, yPos, 100, 25), $"{editingSettings.autoSaveInterval:F0} seconds");
                yPos += 30;
            }

            return yPos;
        }

        private float DrawPathsSection(float yPos, float width)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 30), "Paths");
            yPos += 35;

            DrawLabeledTextField("Unity Editor Path:", editingSettings.UnityEditorPath,
                value => editingSettings.UnityEditorPath = value, yPos, width);
            yPos += 30;

            DrawLabeledTextField("T2G Plugin Path:", editingSettings.T2G_UnityPluginPath,
                value => editingSettings.T2G_UnityPluginPath = value, yPos, width);
            yPos += 30;

            DrawLabeledTextField("Asset Library Path:", editingSettings.AssetLibraryRootPath,
                value => editingSettings.AssetLibraryRootPath = value, yPos, width);
            yPos += 30;

            return yPos;
        }

        private void DrawActionButtons(float windowX, float buttonY)
        {
            float buttonWidth = 100f;

            GUI.backgroundColor = buttonColor;

            // Apply button
            if (GUI.Button(new Rect(windowX + 30, buttonY, buttonWidth, 40), "Apply"))
            {
                ApplyChanges();
                HideWindow();
            }

            // Cancel button
            if (GUI.Button(new Rect(windowX + 450, buttonY, buttonWidth, 40), "Cancel"))
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
            GUI.Label(new Rect(0, yPos, 150, 25), label);
            string newValue = GUI.TextField(new Rect(160, yPos, width - 160, 25), currentValue);
            setter(newValue);
            return yPos + 30;
        }

        private Color DrawColorPicker(Rect rect, Color color)
        {
            // Simple color picker with RGB sliders
            float sliderY = rect.y;

            // R
            GUI.Label(new Rect(rect.x, sliderY, 20, 20), "R:");
            color.r = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 20), color.r, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 20), $"{color.r:F2}");

            sliderY += 25;

            // G
            GUI.Label(new Rect(rect.x, sliderY, 20, 20), "G:");
            color.g = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 20), color.g, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 20), $"{color.g:F2}");

            sliderY += 25;

            // B
            GUI.Label(new Rect(rect.x, sliderY, 20, 20), "B:");
            color.b = GUI.HorizontalSlider(new Rect(rect.x + 25, sliderY, 80, 20), color.b, 0f, 1f);
            GUI.Label(new Rect(rect.x + 110, sliderY, 30, 20), $"{color.b:F2}");

            return color;
        }

        private void ApplyChanges()
        {
            chatBotUI?.ApplyNewSettings(editingSettings);
        }
    }
}