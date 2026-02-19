using UnityEngine;

namespace T2G.Assistant
{
    [RequireComponent(typeof(ChatBotUI))]
    public class ChatBotSimulator : MonoBehaviour
    {
        [SerializeField] private ChatBotUI chatBotUI;
        private string[] testMessages;
        private string[] testResponses;
        [SerializeField] private float messageInterval = 2f;

        private float timer = 0f;
        private int messageIndex = 0;

        private void Start()
        {
            if(chatBotUI == null)
            {
                return;
            }

            if (testMessages == null || testMessages.Length == 0)
            {
                testMessages = new string[]
                {
                    "Hello!",
                    "Who are you?",
                    "Can you help me with game development?",
                    "Thank you for your help!",
                };
            }
            if (testResponses == null || testResponses.Length == 0)
            {
                testResponses = new string[]
                {
                    "Hello! How can I help you?",
                    "I am you assistant",
                    "Yes, definitely!",
                    "You are welcome!",
                };
            }

        }

        private void Update()
        {
            if (messageIndex >= testMessages.Length)
            {
                return;
            }

            timer += Time.deltaTime;

            if (timer >= messageInterval)
            {
                timer = 0f;
                chatBotUI.TestSendMessage(testMessages[messageIndex], testResponses[messageIndex]);
                messageIndex++;
            }
        }
    }
}