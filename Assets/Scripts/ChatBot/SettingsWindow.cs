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
            ComboSelectionField.CloseAll();
        }

        private void DrawSettingsWindow()
        {
            // Handle open dropdown input before any control is drawn, so the
            // dropdown overlay always wins clicks over the settings content.
            ComboSelectionField.HandleInput();

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
            yPos = DrawAISettingsSection(yPos, content.width, viewport, scrollPosition);

            GUI.EndScrollView();

            // Action buttons
            DrawActionButtons(windowX, windowY + windowHeight - 50);

            // Dropdowns are drawn as a screen-space overlay after the scroll view,
            // otherwise they get clipped at the viewport edge.
            ComboSelectionField.DrawOpenDropdown();
        }

        private float CalculateContentHeight()
        {
            float height = 0;

            // Basic settings: header + bot/user name fields
            height += 42 + (2 * 36);

            // Color settings: header + 2 color pickers (label row + 3 sliders each)
            height += 42 + (2 * (36 + 60));

            // Auto-save: header + toggle (+ interval slider when enabled)
            height += 42 + 36;
            if (editingSettings != null && editingSettings.autoSaveHistory)
                height += 36;

            // Paths: header + 3 path fields
            height += 42 + (3 * 36);

            // Spacer before AI settings
            height += 36;

            // AI settings: header + 3 toggles + gap + model row
            height += 36 + (3 * 36) + 42 + 30;

            return height + 12; // Extra padding
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

        private float DrawAISettingsSection(float yPos, float width, Rect scrollViewport, Vector2 scrollOffset)
        {
            DrawSectionHeader(new Rect(0, yPos, width, 36), "AI Settings");
            yPos += 36;

            DrawBoolToggleField("Enable GemeDesc state reference", editingSettings.EnableGameDesc,
                value => editingSettings.EnableGameDesc = value, yPos, width);
            yPos += 36;

            DrawBoolToggleField("Enable Templates", editingSettings.EnableGameDescTemplates,
                value => editingSettings.EnableGameDescTemplates = value, yPos, width);
            yPos += 36;

            DrawBoolToggleField("Enable skills", editingSettings.EnableSkills,
                value => editingSettings.EnableSkills = value, yPos, width);
            yPos += 42;

            yPos = ComboSelectionField.DrawComboSelectionField("Model:", editingSettings.LLModel,
                value => editingSettings.LLModel = value, yPos, width,
                scrollViewport: scrollViewport, scrollOffset: scrollOffset);

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
            var cbStyle = new GUIStyle(GUI.skin.toggle)
            {
                fixedWidth = width,
                fixedHeight = 20,
                alignment = TextAnchor.MiddleLeft,
            };

            float toggleY = yPos + (36f - 20f) / 2f;
            bool newValue = GUI.Toggle(new Rect(0, toggleY, width, 20), currentValue, " " + label, cbStyle);

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
            "qwen3-4B-Instruct-2507",
            "meta-lama3.1-8b",
            "gemma-3-4b-it",
        };

        private const float LabelWidth = 150f;
        private const float FieldGap = 10f;
        private const float RowHeight = 30f;
        private const float ItemHeight = 60f;
        private const float MaxDropdownHeight = 300f;

        // Per-field combo state. Key by a stable ID so multiple calls don't collide.
        private static readonly System.Collections.Generic.Dictionary<string, ComboState> _states
            = new System.Collections.Generic.Dictionary<string, ComboState>();

        private class ComboState
        {
            public bool IsOpen;
            public int OpenFrame = -1;
            public int PressedIndex = -1;
            public Rect ButtonScreenRect;
            public Rect DropdownRect;
            public Vector2 Scroll;
            public string[] Options;
            public string CurrentValue;
            public Action<string> Setter;
        }

        /// <summary>True while any combo dropdown is expanded.</summary>
        public static bool IsAnyOpen
        {
            get
            {
                foreach (var state in _states.Values)
                    if (state.IsOpen) return true;
                return false;
            }
        }

        /// <summary>Closes every expanded dropdown (called when the window hides).</summary>
        public static void CloseAll()
        {
            foreach (var state in _states.Values)
            {
                state.IsOpen = false;
                state.PressedIndex = -1;
            }
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
        /// <param name="scrollViewport">
        /// Screen-space viewport of the enclosing scroll view. When set, the field's
        /// local position is converted to screen space so the dropdown can be drawn
        /// outside the scroll view (see <see cref="DrawOpenDropdown"/>).
        /// </param>
        /// <param name="scrollOffset">Current scroll offset of the enclosing scroll view.</param>
        public static float DrawComboSelectionField(
            string label,
            string currentValue,
            Action<string> setter,
            float yPos,
            float width,
            string[] options = null,
            string fieldKey = null,
            Rect scrollViewport = default,
            Vector2 scrollOffset = default)
        {
            EnsureStyles();

            options = options ?? ModelOptions;
            fieldKey = fieldKey ?? label;

            if (!_states.TryGetValue(fieldKey, out var state))
            {
                state = new ComboState();
                _states[fieldKey] = state;
            }

            state.Options = options;
            state.CurrentValue = currentValue;
            state.Setter = setter;

            // --- Label ---
            GUI.Label(new Rect(0, yPos, LabelWidth, RowHeight), label);

            // --- Button ---
            float fieldX = LabelWidth + FieldGap;
            float fieldW = width - fieldX;
            Rect buttonRect = new Rect(fieldX, yPos, fieldW, RowHeight);

            // GUI rects inside a scroll view live in content space. Remember the
            // screen-space rect so the overlay dropdown can be anchored correctly.
            state.ButtonScreenRect = scrollViewport.height > 0f
                ? new Rect(
                    scrollViewport.x + buttonRect.x - scrollOffset.x,
                    scrollViewport.y + buttonRect.y - scrollOffset.y,
                    buttonRect.width,
                    buttonRect.height)
                : buttonRect;

            string display = string.IsNullOrEmpty(currentValue) ? "(none)" : currentValue;

            if (GUI.Button(buttonRect, display + "  ▼", _buttonStyle))
            {
                bool shouldOpen = !state.IsOpen;
                CloseAll(); // only one dropdown at a time
                state.IsOpen = shouldOpen;
                state.OpenFrame = Time.frameCount;
                state.PressedIndex = -1;
                state.Scroll = Vector2.zero;
            }

            if (state.IsOpen)
                UpdateDropdownRect(state, fieldW);

            return yPos + RowHeight;
        }

        /// <summary>
        /// Consumes mouse events belonging to an open dropdown. Call before drawing
        /// any other control so the dropdown takes priority over the settings UI.
        /// </summary>
        public static void HandleInput()
        {
            var e = Event.current;
            if (e == null) return;

            foreach (var state in _states.Values)
            {
                if (!state.IsOpen || state.Options == null) continue;

                Rect dropdown = state.DropdownRect;
                Vector2 mouse = e.mousePosition;

                if (e.type == EventType.MouseDown)
                {
                    if (dropdown.Contains(mouse))
                    {
                        state.PressedIndex = GetItemIndexAt(state, mouse);
                        e.Use();
                    }
                    else if (Time.frameCount != state.OpenFrame && !state.ButtonScreenRect.Contains(mouse))
                    {
                        // Click outside closes the dropdown without letting the
                        // click reach the control underneath.
                        state.IsOpen = false;
                        state.PressedIndex = -1;
                        e.Use();
                    }
                }
                else if (e.type == EventType.MouseUp)
                {
                    if (state.PressedIndex >= 0)
                    {
                        int pressed = state.PressedIndex;
                        state.PressedIndex = -1;

                        if (GetItemIndexAt(state, mouse) == pressed)
                        {
                            string selected = state.Options[pressed];
                            if (selected != state.CurrentValue)
                            {
                                state.CurrentValue = selected;
                                state.Setter?.Invoke(selected);
                            }
                        }

                        state.IsOpen = false;
                        e.Use();
                    }
                }
                else if (e.type == EventType.ScrollWheel && dropdown.Contains(mouse))
                {
                    float viewportHeight = dropdown.height - 2f;
                    float contentHeight = state.Options.Length * ItemHeight;
                    float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
                    state.Scroll.y = Mathf.Clamp(state.Scroll.y + e.delta.y * 20f, 0f, maxScroll);
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Draws expanded dropdowns in screen space. Call after the settings window
        /// (and its scroll view) has been drawn so the list is not clipped.
        /// </summary>
        public static void DrawOpenDropdown()
        {
            EnsureStyles();

            foreach (var state in _states.Values)
            {
                if (!state.IsOpen || state.Options == null) continue;

                Rect dropdown = state.DropdownRect;
                GUI.Box(dropdown, GUIContent.none, _dropdownBg);

                // Clip the list to the dropdown box. Input inside this area is
                // handled by HandleInput, so items are drawn without controls.
                Rect listViewport = new Rect(
                    dropdown.x + 1f,
                    dropdown.y + 1f,
                    dropdown.width - 2f,
                    dropdown.height - 2f);

                GUI.BeginGroup(listViewport);

                for (int i = 0; i < state.Options.Length; i++)
                {
                    float itemY = i * ItemHeight - state.Scroll.y;
                    if (itemY > listViewport.height || itemY + ItemHeight < 0f)
                        continue;

                    Rect itemRect = new Rect(0f, itemY, listViewport.width, ItemHeight);
                    bool isCurrent = state.Options[i] == state.CurrentValue;
                    bool isPressed = state.PressedIndex == i;

                    var prevBg = GUI.backgroundColor;
                    if (isPressed) GUI.backgroundColor = new Color(0.15f, 0.3f, 0.55f);
                    else if (isCurrent) GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);

                    GUI.Box(itemRect, state.Options[i], _itemStyle);

                    GUI.backgroundColor = prevBg;
                }

                GUI.EndGroup();
            }
        }

        private static void UpdateDropdownRect(ComboState state, float width)
        {
            float ddH = Mathf.Min(state.Options.Length * ItemHeight + 4f, MaxDropdownHeight);
            float ddY = state.ButtonScreenRect.y + state.ButtonScreenRect.height;

            // If the list would run off the bottom of the screen, open upward.
            if (ddY + ddH > Screen.height)
                ddY = state.ButtonScreenRect.y - ddH;

            ddY = Mathf.Clamp(ddY, 2f, Mathf.Max(2f, Screen.height - ddH - 2f));

            state.DropdownRect = new Rect(state.ButtonScreenRect.x, ddY, width, ddH);
        }

        private static int GetItemIndexAt(ComboState state, Vector2 mousePosition)
        {
            Rect listViewport = new Rect(
                state.DropdownRect.x + 1f,
                state.DropdownRect.y + 1f,
                state.DropdownRect.width - 2f,
                state.DropdownRect.height - 2f);

            if (!listViewport.Contains(mousePosition)) return -1;

            float localY = mousePosition.y - listViewport.y + state.Scroll.y;
            int index = Mathf.FloorToInt(localY / ItemHeight);

            return index >= 0 && index < state.Options.Length ? index : -1;
        }
    }
}