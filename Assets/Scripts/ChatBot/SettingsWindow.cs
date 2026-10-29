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

            yPos += 36;
            yPos = DrawAISettingsSection(yPos, content.width);

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

        private float DrawAISettingsSection(float yPos, float width)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 36), "AI Settings");
            yPos += 42;

            ComboSelectionField.DrawComboSelectionField("Model:", editingSettings.UsingLLMModel,
                value => editingSettings.UsingLLMModel = value, yPos, width);
            yPos += 36;

            DrawBoolToggleField("GemeDesc:", editingSettings.EnableGameDesc,
                value => editingSettings.EnableGameDesc = value, yPos, width);
            yPos += 36;

            DrawBoolToggleField("Templates:", editingSettings.EnableGameDescTemplates,
                value => editingSettings.EnableGameDescTemplates = value, yPos, width);
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


        private float DrawBoolToggleField(string label, bool currentValue, Action<bool> setter, float yPos, float width)
        {
            GUI.Label(new Rect(0, yPos, 150, 30), label);

            var cbStyle = new GUIStyle(GUI.skin.toggle)
            {
                fixedWidth = 132,
                fixedHeight = 20,
                alignment = TextAnchor.MiddleCenter,
            };

            float toggleY = yPos + (36f - 20f) / 2f;
            bool newValue = GUI.Toggle(new Rect(160, toggleY, width - 160, 20), currentValue, "Enabled", cbStyle);

            setter(newValue);
            return yPos + 36;
        }

        private void ApplyChanges()
        {
            chatBotUI?.SaveSettings(editingSettings);
        }
    }

    public static class ComboSelectionField
    {
        // The selectable options for this specific field
        public static readonly string[] ModelOptions = new[]
        {
        "qwen3.5-9b",
        "llama-3.2-3b-instruct",
        "qwen3-4B-Instruct-2507",
        "qwen3.5-4B",
        "gemma-3-4b-it",
    };

        private const float LabelWidth = 150f;
        private const float FieldGap = 10f;
        private const float RowHeight = 30f;
        private const float ItemHeight = 24f;
        private const float MaxDropdownHeight = 200f;

        // Per-field combo state. Key by a stable ID so multiple calls don't collide.
        private static readonly System.Collections.Generic.Dictionary<string, ComboState> _states
            = new System.Collections.Generic.Dictionary<string, ComboState>();

        private class ComboState
        {
            public bool IsOpen;
            public int OpenFrame = -1;
            public Rect DropdownRect;
            public Vector2 Scroll;
        }

        private static GUIStyle _buttonStyle;
        private static GUIStyle _itemStyle;
        private static GUIStyle _dropdownBg;

        private static void EnsureStyles()
        {
            if (_buttonStyle != null) return;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 20, 0, 0)
            };

            _itemStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _dropdownBg = new GUIStyle(GUI.skin.box);
        }

        /// <summary>
        /// Draws a labeled combo box for selecting a string from a fixed list.
        /// Returns the next yPos.
        /// </summary>
        /// <param name="label">Row label.</param>
        /// <param name="currentValue">Current selected value.</param>
        /// <param name="setter">Called only when selection actually changes.</param>
        /// <param name="yPos">Row y position.</param>
        /// <param name="width">Available width for this row.</param>
        /// <param name="options">Optional override list; defaults to <see cref="ModelOptions"/>.</param>
        /// <param name="fieldKey">Unique key for state isolation; defaults to label.</param>
        public static float DrawComboSelectionField(
            string label,
            string currentValue,
            Action<string> setter,
            float yPos,
            float width,
            string[] options = null,
            string fieldKey = null)
        {
            EnsureStyles();

            options = options ?? ModelOptions;
            fieldKey = fieldKey ?? label;

            if (!_states.TryGetValue(fieldKey, out var state))
            {
                state = new ComboState();
                _states[fieldKey] = state;
            }

            // --- Label ---
            GUI.Label(new Rect(0, yPos, LabelWidth, RowHeight), label);

            // --- Button ---
            float fieldX = LabelWidth + FieldGap;
            float fieldW = width - fieldX;
            Rect buttonRect = new Rect(fieldX, yPos, fieldW, RowHeight);

            string display = string.IsNullOrEmpty(currentValue) ? "(none)" : currentValue;

            if (GUI.Button(buttonRect, display + "  ▼", _buttonStyle))
            {
                state.IsOpen = !state.IsOpen;
                state.OpenFrame = Time.frameCount;

                // Position dropdown below button
                float ddH = Mathf.Min(options.Length * ItemHeight + 4f, MaxDropdownHeight);
                state.DropdownRect = new Rect(buttonRect.x, buttonRect.y + buttonRect.height, fieldW, ddH);

                // If dropdown would go off bottom of screen, open upward
                if (state.DropdownRect.yMax > Screen.height)
                    state.DropdownRect.y = buttonRect.y - state.DropdownRect.height;
            }

            // --- Dropdown ---
            if (state.IsOpen)
            {
                // Register a GUI group so clicks inside are captured
                GUI.Box(state.DropdownRect, GUIContent.none, _dropdownBg);

                Rect innerViewport = new Rect(
                    state.DropdownRect.x + 1,
                    state.DropdownRect.y + 1,
                    state.DropdownRect.width - 2,
                    state.DropdownRect.height - 2);

                Rect content = new Rect(
                    0, 0,
                    innerViewport.width - (options.Length * ItemHeight + 4f > MaxDropdownHeight ? 16f : 0f),
                    options.Length * ItemHeight);

                state.Scroll = GUI.BeginScrollView(innerViewport, state.Scroll, content);

                for (int i = 0; i < options.Length; i++)
                {
                    Rect itemRect = new Rect(0, i * ItemHeight, content.width, ItemHeight);
                    bool isCurrent = options[i] == currentValue;

                    // Highlight current item
                    var prevBg = GUI.backgroundColor;
                    if (isCurrent) GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);

                    if (GUI.Button(itemRect, options[i], _itemStyle))
                    {
                        if (options[i] != currentValue)
                            setter?.Invoke(options[i]);

                        state.IsOpen = false;
                    }

                    GUI.backgroundColor = prevBg;
                }

                GUI.EndScrollView();

                // Close on outside click (but not on the frame it was opened)
                if (Time.frameCount != state.OpenFrame &&
                    Event.current.type == EventType.MouseDown &&
                    !state.DropdownRect.Contains(Event.current.mousePosition) &&
                    !buttonRect.Contains(Event.current.mousePosition))
                {
                    state.IsOpen = false;
                    Event.current.Use();
                }
            }

            return yPos + RowHeight;
        }
    }
}