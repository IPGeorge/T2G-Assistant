using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace T2G.Assistant
{
    public class ChatBotUI : MonoBehaviour
    {
        private readonly string k_SettingsFileName = "Settings.json";
        private readonly string k_IntentsFileName = "FrequentIntents.json";
        private readonly int MaxInputHistory = 50;
        private readonly int MaxFrequentIntents = 5;
        private readonly string k_Working = "Working ...";

        ComboBox _frequentUsedCommandsComboBox = new ComboBox();

        private struct ChatMessage
        {
            public string sender;
            public string message;
            public DateTime timestamp;

            public ChatMessage(string sender, string message)
            {
                this.sender = sender;
                this.message = message;
                this.timestamp = DateTime.Now;
            }
        }

        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        private string fullHistoryString = "";
        private string currentInput = "";
        private bool gotoBottom = true;
        private Vector2 scrollPosition = Vector2.zero;
        private float chatWindowHeight = 500.0f;
        private float messageLineHeight = 0.0f;

        private List<string> inputHistory = new List<string>();
        private int inputHistoryIndex = -1;
        private string inputHistoryBuffer = "";

        private Dictionary<string, int> intentFrequency = new Dictionary<string, int>();
        private List<string> frequentIntents = new List<string>();
        private float comboWidth = 150f;
        private int selectedIntentIndex = -1;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        [SerializeField] private Color inputFieldColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [Header("Layout")]
        [SerializeField] private readonly int fontSize = 24;
        [SerializeField] private readonly float inputFieldHeight = 65f;

        private string[] botResponses = {
            "Sorry, I don't understand!",
            "I am confused!",
            "Could you express your idea in a different way?",
        };

        public Action<string> OnInput = null;

        public static ChatBotUI Instance { get; private set; } = null;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadSettings();
            LoadFrequentIntents();
            var settings = Assistant.Instance.Settings;
            AddMessage(settings.botName, $"Hello {settings.userName}! I'm {settings.botName}. How can I help you today?");
        }

        private void OnGUI()
        {
            if(SettingsWindow.Instance == null || SettingsWindow.Instance.IsWindowVisible)
            {
                return;
            }

            GUI.skin.box.normal.background = Texture2D.grayTexture;

            GUI.skin.label.fontSize = fontSize;
            GUI.skin.textField.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.box.fontSize = fontSize;

            DrawChatWindow();
            DrawInputArea();
            DrawControlButtons();
        }

        private void DrawChatWindow()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float windowY = 20f;
            chatWindowHeight = Screen.height - 150.0f;

            GUI.backgroundColor = backgroundColor;
            GUI.Box(new Rect(windowX, windowY, windowWidth, chatWindowHeight), "");

            Rect viewport = new Rect(windowX + 10, windowY + 10, windowWidth - 30, chatWindowHeight - 20);
            
            float totalContentHeight = CalculateTotalContentHeight(windowWidth - 50);
            Rect contentRect = new Rect(0, 0, windowWidth - 50, totalContentHeight);

            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, contentRect);

            float yPos = 0;
            foreach (var message in chatHistory)
            {
                DrawMessageBubble(message, ref yPos, windowWidth - 50);
            }

            GUI.EndScrollView();

            if (gotoBottom && chatHistory.Count > 0)
            {
                scrollPosition.y = Mathf.Max(0, totalContentHeight - (chatWindowHeight - 20));
                gotoBottom = false;
            }
        }

        private float CalculateTotalContentHeight(float width)
        {
            float totalHeight = 0f;
            GUIStyle style = GUI.skin.box;
            
            foreach (var message in chatHistory)
            {
                GUIContent content = new GUIContent(message.message);
                Vector2 size = style.CalcSize(content);
                size.x = Mathf.Min(size.x + 25, width * 0.7f);
                size.y = style.CalcHeight(content, size.x - 25);
                totalHeight += 30 + size.y + 4;
            }
            
            return Mathf.Max(totalHeight, chatWindowHeight - 20);
        }

        private void DrawMessageBubble(ChatMessage message, ref float yPos, float width)
        {
            var settings = Assistant.Instance.Settings;
            bool isUser = message.sender == settings.userName;
            Color messageColor = isUser ? settings.userMessageColor : settings.botMessageColor;  

            GUIStyle style = GUI.skin.box;
            
            float maxBubbleWidth = width * 0.7f;
            GUIContent content = new GUIContent(message.message);
            Vector2 size = style.CalcSize(content);
            size.x = Mathf.Min(size.x + 40, maxBubbleWidth);
            size.y = style.CalcHeight(content, size.x - 40);

            float xPos = isUser ? width - size.x - 10 : 10;

            string senderLabel = $"{message.sender} - {message.timestamp:HH:mm:ss}";
            Vector2 labelSize = style.CalcSize(new GUIContent(senderLabel));
            
            Rect senderRect;
            if (isUser)
            {
                senderRect = new Rect(width - labelSize.x - 1.0f, yPos, size.x, 28);
            }
            else
            {
                senderRect = new Rect(xPos, yPos, size.x, 28);
            }
            GUI.color = messageColor;
            GUI.Label(senderRect, senderLabel);
            yPos += 30;

            Rect messageRect = new Rect(xPos, yPos, size.x, size.y);
            GUI.color = messageColor;
            GUIStyle messageStyle = GUI.skin.box;
            messageStyle.alignment = isUser ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            messageStyle.wordWrap = true;
            GUI.Box(messageRect, message.message, messageStyle);
            GUI.color = Color.white;

            yPos += size.y + 4;
            messageLineHeight = 28 + size.y + 6;
            GUI.FocusControl("ChatInput");
        }

        private void DrawInputArea()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float inputY = 20f + chatWindowHeight + 15f;

            GUI.backgroundColor = inputFieldColor;
            GUI.Box(new Rect(windowX, inputY, windowWidth, inputFieldHeight), "");
            GUI.backgroundColor = Color.white;

            //Rect comboRect = new Rect(windowX + 10, inputY + 10, windowWidth - 100, inputFieldHeight - 20);

            //if (frequentIntents.Count > 0)
            //{
            //    string[] intentDisplay = new string[frequentIntents.Count + 1];
            //    intentDisplay[0] = "Recent Inputs";

            //    for (int i = 0; i < frequentIntents.Count; i++)
            //    {
            //        string intent = frequentIntents[i];
            //        int displayLength = Mathf.Min(intent.Length, 20);
            //        intentDisplay[i + 1] = intent.Substring(0, displayLength) + (intent.Length > 20 ? "..." : "");
            //    }

            //    _frequentUsedCommandsComboBox.Draw(comboRect, intentDisplay, "Recents");

            //    int selectedIndex = 1;
            //    if (selectedIndex > 0)
            //    {
            //        currentInput = frequentIntents[selectedIndex - 1];
            //        GUI.FocusControl("ChatInput");
            //    }
            //}

            //GUI.SetNextControlName("ChatInput");

            float textFieldX = windowX + 10;
            float textFieldWidth = windowWidth - 190;
            
            Rect inputRect = new Rect(textFieldX, inputY + 10, textFieldWidth, inputFieldHeight - 20);
            
            GUI.SetNextControlName("ChatInput");
            currentInput = GUI.TextField(inputRect, currentInput);

            Event currentEvent = Event.current;
            string focusedControl = GUI.GetNameOfFocusedControl();
            if (focusedControl == "ChatInput")
            {
                if (currentEvent.type == EventType.KeyDown & currentEvent.character == '\n')
                {
                    SendMessage();
                    currentEvent.Use();
                    return;
                }
                else if (currentEvent.isKey && currentEvent.keyCode == KeyCode.UpArrow)
                {
                    NavigateInputHistory(-1);
                    currentEvent.Use();
                }
                else if (currentEvent.isKey && currentEvent.keyCode == KeyCode.DownArrow)
                {
                    NavigateInputHistory(1);
                    currentEvent.Use();
                }
            }

            if (GUI.Button(new Rect(windowX + windowWidth - 160, inputY + 12, 150, inputFieldHeight - 24), "Send") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return &&
                 GUI.GetNameOfFocusedControl() == "ChatInput"))
            {
                SendMessage();
                GUI.FocusControl("ChatInput");
            }
        }

        public void UpdateLastBotMessage(string message)
        {
            if (chatHistory.Count > 0 &&
                string.Equals(chatHistory[chatHistory.Count - 1].sender, Assistant.Instance.Settings.botName))
            {
                chatHistory[chatHistory.Count - 1] = new ChatMessage(Assistant.Instance.Settings.botName, message);
                gotoBottom = true;
            }
        }

        private void NavigateInputHistory(int direction)
        {
            if (inputHistory.Count == 0) return;

            if (inputHistoryIndex == -1)
            {
                inputHistoryBuffer = currentInput;
                inputHistoryIndex = inputHistory.Count - 1;
            }
            else
            {
                inputHistoryIndex += direction;
            }

            if (inputHistoryIndex < 0)
            {
                inputHistoryIndex = -1;
                currentInput = inputHistoryBuffer;
            }
            else if (inputHistoryIndex >= inputHistory.Count)
            {
                inputHistoryIndex = -1;
                currentInput = inputHistoryBuffer;
            }
            else
            {
                currentInput = inputHistory[inputHistoryIndex];
            }
        }

        private void DrawControlButtons()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float buttonsY = 20f + chatWindowHeight + inputFieldHeight + 20f;

            float buttonWidth = 150f;
            float buttonHeight = 40f;
            float spacing = 12f;

            if (GUI.Button(new Rect(windowX, buttonsY, buttonWidth, buttonHeight), "Clear"))
            {
                ClearChat();
            }

            if (GUI.Button(new Rect(windowX + (buttonWidth + spacing) * 1, buttonsY, buttonWidth, buttonHeight), "Settings"))
            {
                SettingsWindow.Instance.ShowWindow();
            }

            if (GUI.Button(new Rect(windowX + (buttonWidth + spacing) * 2, buttonsY, buttonWidth, buttonHeight), "Data Dir"))
            {
                string persistantPath = Application.persistentDataPath.Replace("/", "\\");
                System.Diagnostics.Process.Start("explorer.exe", persistantPath);
            }

            GUI.Label(new Rect(windowX + (buttonWidth + spacing) * 3, buttonsY, buttonWidth * 3, buttonHeight),
                Assistant.Instance.Settings.DefaultUnityProject);
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(currentInput))
            {
                return;
            }

            string input = currentInput;
            currentInput = string.Empty;
            AddMessage(Assistant.Instance.Settings.userName, input);

            AddToInputHistory(input);
            UpdateIntentFrequency(input);

            AddMessage(Assistant.Instance.Settings.botName, k_Working);

            var result = await Assistant.Instance.ProcessEnteredIntent(input);

            Debug.Log($"[ChatBotUI] ProcessEnteredIntent result: succeeded={result.succeeded}, response='{result.response}'");
            if (result.succeeded)
            {
                AddMessage(Assistant.Instance.Settings.botName, result.response);
            }
            else
            {
                string response = result.response ?? GenerateBotResponse();
                AddMessage(Assistant.Instance.Settings.botName, response);
            }

            inputHistoryIndex = -1;
            inputHistoryBuffer = "";
            GUI.FocusControl("ChatInput");
        }

        private void AddToInputHistory(string input)
        {
            if (inputHistory.Count == 0 || inputHistory[inputHistory.Count - 1] != input)
            {
                inputHistory.Add(input);
                if (inputHistory.Count > MaxInputHistory)
                {
                    inputHistory.RemoveAt(0);
                }
            }
        }

        private void UpdateIntentFrequency(string intent)
        {
            string normalizedIntent = intent.Trim().ToLower();
            if (intentFrequency.ContainsKey(normalizedIntent))
            {
                intentFrequency[normalizedIntent]++;
            }
            else
            {
                intentFrequency[normalizedIntent] = 1;
            }

            frequentIntents = intentFrequency
                .OrderByDescending(x => x.Value)
                .Take(MaxFrequentIntents)
                .Select(x => x.Key)
                .ToList();

            SaveFrequentIntents();
        }

        private void AddMessage(string sender, string message)
        {
            int cnt = chatHistory.Count;
            if (cnt > 0 && string.Compare(chatHistory[cnt - 1].message, k_Working, true) == 0)
            {
                chatHistory.RemoveAt(cnt - 1);
            }

            ChatMessage newMessage = new ChatMessage(sender, message);
            chatHistory.Add(newMessage);

            gotoBottom = true;

            fullHistoryString += $"[{newMessage.timestamp:yyyy-MM-dd HH:mm:ss}] {sender}: {message}\n";
            OnInput?.Invoke(message);
        }

        private string GenerateBotResponse()
        {
            return botResponses[UnityEngine.Random.Range(0, botResponses.Length)];
        }

        public void ClearChat()
        {
            chatHistory.Clear();
            fullHistoryString = "Chat cleared at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
            AddMessage(Assistant.Instance.Settings.botName, "Chat history has been cleared.");
        }

        private void SaveFrequentIntents()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, k_IntentsFileName);
                string json = JsonConvert.SerializeObject(intentFrequency);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save frequent intents: {e.Message}");
            }
        }

        private void LoadFrequentIntents()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, k_IntentsFileName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    intentFrequency = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                    frequentIntents = intentFrequency
                        .OrderByDescending(x => x.Value)
                        .Take(MaxFrequentIntents)
                        .Select(x => x.Key)
                        .ToList();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load frequent intents: {e.Message}");
                intentFrequency = new Dictionary<string, int>();
            }
        }

        public void SaveSettings(Settings newSettings = null)
        {
            if (newSettings != null)
            {
                Utils.CopySettings(newSettings, Assistant.Instance.Settings);
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(Assistant.Instance.Settings);
            AssetDatabase.SaveAssets();
#endif
            T2G_Settings t2g_settings = new T2G_Settings();
            t2g_settings.CopyFrom(Assistant.Instance.Settings);
            string json = JsonConvert.SerializeObject(t2g_settings);
            string path = Path.Combine(Application.persistentDataPath, k_SettingsFileName);
            File.WriteAllText(path, json);
            AddMessage(Assistant.Instance.Settings.botName, "Settings have been updated!");
        }

        public void LoadSettings()
        {
            string path = Path.Combine(Application.persistentDataPath, k_SettingsFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loadedSettings = JsonConvert.DeserializeObject<T2G_Settings>(json);
                loadedSettings.CopyTo(Assistant.Instance.Settings);
            }
        }

        public void TestSendMessage(string message, string response)
        {
            var settings = Assistant.Instance.Settings;
            AddMessage(settings.userName, message);
            AddMessage(settings.botName, response);
        }
    }

    public class ComboBox
    {
        private bool isExpanded = false;
        private int selectedIndex = -1;
        private string selectedText = "";
        private Rect dropdownRect;

        public string SelectedText => selectedText;
        public int SelectedIndex => selectedIndex;

        public bool Draw(Rect rect, string[] items, string defaultText = "Select...")
        {
            bool changed = false;

            // Draw the combo box button
            string buttonText = selectedIndex >= 0 ? items[selectedIndex] : defaultText;
            if (GUI.Button(rect, buttonText + (isExpanded ? " ▲" : " ▼"), GUI.skin.box))
            {
                isExpanded = !isExpanded;
                if (isExpanded)
                {
                    dropdownRect = new Rect(rect.x, rect.y + rect.height, rect.width,
                                           Mathf.Min(200, items.Length * 25));
                }
            }

            // Draw dropdown if expanded
            if (isExpanded && items.Length > 0)
            {
                GUI.Box(dropdownRect, "");

                for (int i = 0; i < items.Length; i++)
                {
                    Rect itemRect = new Rect(dropdownRect.x + 2, dropdownRect.y + 2 + (i * 25),
                                             dropdownRect.width - 4, 25);

                    if (itemRect.Contains(Event.current.mousePosition))
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                    }

                    if (GUI.Button(itemRect, items[i], GUI.skin.label))
                    {
                        selectedIndex = i;
                        selectedText = items[i];
                        isExpanded = false;
                        changed = true;
                    }

                    GUI.backgroundColor = Color.white;
                }

                // Close when clicking outside
                if (Event.current.type == EventType.MouseDown &&
                    !rect.Contains(Event.current.mousePosition) &&
                    !dropdownRect.Contains(Event.current.mousePosition))
                {
                    isExpanded = false;
                }
            }

            return changed;
        }

        public void Reset()
        {
            selectedIndex = -1;
            selectedText = "";
            isExpanded = false;
        }
    }
}
