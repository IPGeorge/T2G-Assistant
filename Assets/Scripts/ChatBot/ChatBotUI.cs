using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace T2G.Assistant
{
    public class ChatBotUI : MonoBehaviour
    {
        private readonly string k_SettingsFileName = "Settings.json";

        // Chat history storage
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
        private string fullHistoryString = ""; // Complete communication history in string format
        private string currentInput = "";
        private bool gotoBottom = true;
        private Vector2 scrollPosition = Vector2.zero;
        private float chatWindowHeight = 500.0f;
        private float messageLineHeight = 0.0f;

        // UI Settings
        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        [SerializeField] private Color inputFieldColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [Header("Layout")]
        [SerializeField] private readonly int fontSize = 12;
        [SerializeField] private readonly float inputFieldHeight = 42f;

        // Bot responses (simple example - you can expand or connect to AI)
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
            var settings = Assistant.Instance.Settings;
            // Add welcome message
            AddMessage(settings.botName, $"Hello {settings.userName}! I'm {settings.botName}. How can I help you today?");
        }

        private void OnGUI()
        {
            if(SettingsWindow.Instance.IsWindowVisible)
            {
                return;
            }

            GUI.skin.box.normal.background = Texture2D.grayTexture;

            // Set GUI style
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.textField.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;
            GUI.skin.box.fontSize = fontSize;

            // Main chat window area
            DrawChatWindow();

            // Input area
            DrawInputArea();

            // Control buttons
            DrawControlButtons();
        }

        private void DrawChatWindow()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float windowY = 20f;
            chatWindowHeight = Screen.height - 120.0f;

            // Chat background
            GUI.backgroundColor = backgroundColor;
            GUI.Box(new Rect(windowX, windowY, windowWidth, chatWindowHeight), "");

            // Chat messages area with scroll
            Rect viewport = new Rect(windowX + 10, windowY + 10, windowWidth - 30, chatWindowHeight - 20);
            Rect contentRect = new Rect(0, 0, windowWidth - 50, chatHistory.Count * messageLineHeight);

            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, contentRect);

            float yPos = 0;
            foreach (var message in chatHistory)
            {
                DrawMessageBubble(message, ref yPos, windowWidth - 50);
            }

            GUI.EndScrollView();

            // Auto-scroll to bottom when new messages are added
            if (gotoBottom && chatHistory.Count > 0)
            {
                contentRect.height = yPos;
                scrollPosition.y = Mathf.Max(0, yPos - chatWindowHeight + 40);
                gotoBottom = false;
            }
        }

        private void DrawMessageBubble(ChatMessage message, ref float yPos, float width)
        {
            var settings = Assistant.Instance.Settings;
            bool isUser = message.sender == settings.userName;
            Color messageColor = isUser ? settings.userMessageColor : settings.botMessageColor;  

            // Calculate message size
            GUIContent content = new GUIContent(message.message);
            GUIStyle style = GUI.skin.box;
            Vector2 size = style.CalcSize(content);
            size.x = Mathf.Min(size.x + 20, width * 0.7f);
            size.y = style.CalcHeight(content, size.x - 20);

            // Position (user on right, bot on left)
            float xPos = isUser ? width - size.x - 10 : 10;

            // Sender label
            string senderLabel = $"{message.sender} - {message.timestamp:HH:mm:ss}";
            Rect senderRect;
            if (isUser)
            {
                Vector2 labelSize = style.CalcSize(new GUIContent(senderLabel));
                float posX = width - labelSize.x - 1.0f;
                senderRect = new Rect(posX, yPos, size.x, 20);
            }
            else
            {
                senderRect = new Rect(xPos, yPos, size.x, 20);
            }
            GUI.color = messageColor;
            GUI.Label(senderRect, senderLabel);
            yPos += 20;

            // Message bubble
            Rect messageRect = new Rect(xPos, yPos, size.x, size.y);
            GUI.color = messageColor;
            GUIStyle messageStyle = GUI.skin.box;
            messageStyle.alignment = isUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            GUI.Box(messageRect, message.message, messageStyle);
            GUI.color = Color.white;

            yPos += size.y + 1;
            messageLineHeight = 20 + size.y + 1;

            GUI.FocusControl("ChatInput");
        }

        private void DrawInputArea()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float inputY = 20f + chatWindowHeight + 10f;

            // Input field background
            GUI.backgroundColor = inputFieldColor;
            GUI.Box(new Rect(windowX, inputY, windowWidth, inputFieldHeight), "");
            GUI.backgroundColor = Color.white;

            // Input field
            GUI.SetNextControlName("ChatInput");
            Rect inputRect = new Rect(windowX + 10, inputY + 10, windowWidth - 120, inputFieldHeight - 20);
            Event currentEvent = Event.current;

            // Draw the text field
            currentInput = GUI.TextField(inputRect, currentInput);

            // Check if this text field has focus and Enter was pressed
            if (currentEvent.type == EventType.KeyDown && currentEvent.character == '\n'
                 && GUI.GetNameOfFocusedControl() == "ChatInput")
            {
                SendMessage();
                currentEvent.Use();  // Clear the event so it doesn't affect other GUI elements
                return;
            }

            // Send button
            if (GUI.Button(new Rect(windowX + windowWidth - 100, inputY + 8, 90, inputFieldHeight - 16), "Send") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return &&
                 GUI.GetNameOfFocusedControl() == "ChatInput"))
            {
                SendMessage();
                GUI.FocusControl("ChatInput");
            }
        }

        private void DrawControlButtons()
        {
            float windowWidth = Screen.width * 0.8f;
            float windowX = Screen.width * 0.1f;
            float buttonsY = 20f + chatWindowHeight + inputFieldHeight + 16f;

            float buttonWidth = 120f;
            float buttonHeight = 32f;
            float spacing = 10f;

            // Clear Chat button
            if (GUI.Button(new Rect(windowX, buttonsY, buttonWidth, buttonHeight), "Clear Chat"))
            {
                ClearChat();
            }

            // Save History button
            if (GUI.Button(new Rect(windowX + buttonWidth + spacing, buttonsY, buttonWidth, buttonHeight), "Save History"))
            {
                SaveHistoryToFile();
            }

            // Load History button
            if (GUI.Button(new Rect(windowX + (buttonWidth + spacing) * 2, buttonsY, buttonWidth, buttonHeight), "Load History"))
            {
                LoadHistoryFromFile();
            }

            // Export History button
            if (GUI.Button(new Rect(windowX + (buttonWidth + spacing) * 3, buttonsY, buttonWidth, buttonHeight), "Export Text"))
            {
                ExportHistoryToClipboard();
            }

            // Settings
            if (GUI.Button(new Rect(windowX + (buttonWidth + spacing) * 4, buttonsY, buttonWidth, buttonHeight), "Settings"))
            {
                SettingsWindow.Instance.ShowWindow();
            }

        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(currentInput))
            {
                return;
            }

            // Add and display user message 
            AddMessage(Assistant.Instance.Settings.userName, currentInput);

            AddMessage(Assistant.Instance.Settings.botName, "Working ...");     
            var result = await Assistant.Instance.ProcessEnteredIntent(currentInput);
            if (result.succeeded)
            {
                ModifyLastMessage(Assistant.Instance.Settings.botName, result.response);
            }
            else
            {
                string response = result.response ?? GenerateBotResponse();
                ModifyLastMessage(Assistant.Instance.Settings.botName, response);
            }

            // Clear input field
            currentInput = "";
            GUI.FocusControl("ChatInput");
        }

        private System.Collections.IEnumerator DelayedResponse(string response, float delay)
        {
            yield return new WaitForSeconds(delay);
            AddMessage(Assistant.Instance.Settings.botName, response);
        }

        private void AddMessage(string sender, string message)
        {
            ChatMessage newMessage = new ChatMessage(sender, message);
            chatHistory.Add(newMessage);
            gotoBottom = true;

            // Update full history string
            fullHistoryString += $"[{newMessage.timestamp:yyyy-MM-dd HH:mm:ss}] {sender}: {message}\n";

            Debug.Log($"Added message: {sender}: {message}");

            OnInput?.Invoke(message);
        }

        private void ModifyLastMessage(string sender, string message)
        {
            int cnt = chatHistory.Count;
            if (cnt <= 0 ||
                string.Compare(chatHistory[cnt - 1].sender, sender) != 0)
            {
                return;
            }
            chatHistory.RemoveAt(cnt - 1);
            chatHistory.Add(new ChatMessage(sender, message));
        }

        private string GenerateBotResponse()
        {
            // Default: Random response from predefined list
            return botResponses[UnityEngine.Random.Range(0, botResponses.Length)];
        }

        private void ClearChat()
        {
            chatHistory.Clear();
            fullHistoryString = "Chat cleared at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
            AddMessage(Assistant.Instance.Settings.botName, "Chat history has been cleared.");
        }

        private void SaveHistoryToFile()
        {
            var settings = Assistant.Instance.Settings;
            string filename = $"ChatHistory_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);

            try
            {
                System.IO.File.WriteAllText(filepath, fullHistoryString);
                AddMessage(settings.botName, $"Chat history saved to: {filepath}");
                Debug.Log($"History saved to: {filepath}");
            }
            catch (System.Exception e)
            {
                AddMessage(settings.botName, $"Error saving history: {e.Message}");
                Debug.LogError($"Save error: {e.Message}");
            }
        }

        private void LoadHistoryFromFile()
        {
            // This would require file browser implementation
            // For simplicity, we'll just show a message
            AddMessage(Assistant.Instance.Settings.botName, 
                "To implement file loading, you would need to add a file browser. For now, history is stored in memory.");
        }

        private void ExportHistoryToClipboard()
        {
            GUIUtility.systemCopyBuffer = fullHistoryString;
            AddMessage(Assistant.Instance.Settings.botName, "Chat history copied to clipboard!");
        }

        // Public method to get the complete history
        public string GetFullHistory()
        {
            return fullHistoryString;
        }

        // Public method to get message count
        public int GetMessageCount()
        {
            return chatHistory.Count;
        }

        // Example of how to programmatically add a message
        public void TestSendMessage(string message, string response)
        {
            var settings = Assistant.Instance.Settings;
            AddMessage(settings.userName, message);
            AddMessage(settings.botName, response);
        }

        // Public methods for settings integration
        public void ApplyNewSettings(Settings newSettings)
        {
            if (newSettings == null)
            {
                return;
            }

            Utils.CopySettings(newSettings, Assistant.Instance.Settings);
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

    }
}