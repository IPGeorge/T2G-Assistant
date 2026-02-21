using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    public partial class Assistant : MonoBehaviour
    {
        static public Assistant Instance { get; private set; } = null;

        [Header("UI Settings")]
        [SerializeField] private Settings _settings;
        public Settings Settings => _settings;

        private ChatBotUI _chatBot;
        private Translation _tanslation = new Translation();
        private Resolution _resolution = new Resolution();

        public CommunicatorClient Communicator { get; private set; }

        private List<Instruction> _instructions;
        private bool _completed;
        private StringBuilder _sb = new StringBuilder();

        public GameDescManager GameDescManager { get; }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            //Instruction instruction = new Instruction();
            //string json1 = JsonConvert.SerializeObject(instruction, Formatting.Indented);
            //GameDesc gameDesc = new GameDesc();
            //string json2 = JsonConvert.SerializeObject(gameDesc, Formatting.Indented);

            _chatBot = ChatBotUI.Instance;
            Init();
        }

        private void Update()
        {
            Communicator?.UpdateClient();
        }

        private void OnDestroy()
        {
            Uninit();
        }

        private async void Init()
        {
            Communicator = CommunicatorClient.Instance;
            Communicator.OnConnectedToServer += OnConnectedToServer;
            Communicator.OnFailedToConnectToServer += OnFailedToConnectToServer;
            Communicator.OnDisconnecting += OnDisconnecting;
            Communicator.OnDisconnected += OnDisconnectedFromServer;
            Communicator.OnSentMessage += OnSentMessage;
            Communicator.OnReceivedMessage += OnReceivedMessage;
            Communicator.OnError += OnError;
            await Communicator.StartClient();
        }

        void Uninit()
        {
            Communicator.OnConnectedToServer -= OnConnectedToServer;
            Communicator.OnFailedToConnectToServer -= OnFailedToConnectToServer;
            Communicator.OnDisconnecting -= OnDisconnecting;
            Communicator.OnDisconnected -= OnDisconnectedFromServer;
            Communicator.OnSentMessage -= OnSentMessage;
            Communicator.OnReceivedMessage -= OnReceivedMessage;
            Communicator.OnError -= OnError;
        }

        #region Communicator event handlers
        private void OnConnectedToServer()
        {
            Debug.Log("[Assistant] Connected to the server successfully.");

            //Send settings
            SettingsLite liteSettings = _settings.CloneToSettingsLite();
            string settingsJson = JsonConvert.SerializeObject(liteSettings);
            CommunicatorClient.Instance.SendMessage(CommunicatorBase.eMessageType.Settings, settingsJson);
        }

        private void OnFailedToConnectToServer()
        {
            Debug.Log("[Assistant] Failed to connected to the server!");
        }

        private void OnDisconnecting()
        {
            Debug.Log("[Assistant] Disconnecting ...");
        }

        private void OnDisconnectedFromServer()
        {
            Debug.Log("[Assistant] Disconnected!");
        }

        private void OnSentMessage(string message)
        {
            Debug.Log($"[Assistant] Sent message: {message}");
        }

        private void OnReceivedMessage(CommunicatorBase.eMessageType type, string message)
        {
            Debug.Log($"[Assistant] Received {type.ToString()}: {message}");
        }

        private void OnError(string errorMesasge)
        {
            Debug.LogError($"[Assistant] Error: {errorMesasge}");
        }

 #endregion Communicator event handlers

        async Awaitable<(bool responded, string message)> WaitForResponse()
        {
            int timeoutMiniSeconds = 10000;
            int waitInterval = 100;
            while(Communicator.IsReceiveBufferEmpty)
            {
                await Task.Delay(waitInterval);
                timeoutMiniSeconds -= waitInterval;
                if(timeoutMiniSeconds < 0)
                {
                    return (true, "Timeout waiting for the response!");
                }
            }
            Communicator.RetriveMessageFromReceiveBuffer(out var responseData);
            var response = JsonConvert.DeserializeObject<Response>(responseData.Message.ToString());
            return (response.Succeeded, response.Message);
        }


        void InsertAdditionalInstructions(int i, List<Instruction> additionalInstructions)
        {
            if (i < _instructions.Count - 1)
            {
                _instructions.InsertRange(i + 1, additionalInstructions);
            }
            else
            {
                _instructions.AddRange(additionalInstructions);
            }
        }

        async Awaitable ProcessInstruction(int i)
        {
            var instruction = _instructions[i];
            if (instruction.state == Instruction.eState.Local)
            {
                var result = await LocalExecution.Instance.Execute(instruction);
                _completed &= result.succeeded;
                _sb.AppendLine(result.message);

                if (result.additionalInstructions != null && result.additionalInstructions.Count > 0)
                {
                    InsertAdditionalInstructions(i, result.additionalInstructions);
                }
            }
            else
            {
                if (instruction.state == Instruction.eState.Raw)
                {
                    _resolution.Resolve(ref instruction);  //The returned instruction must be either raw or resolved 
                }

                if (instruction.state == Instruction.eState.Resolved)
                {
                    Communicator.EmptyReceiveBuffer();
                    string jsonInstruction = JsonConvert.SerializeObject(instruction); 
                    Communicator.SendMessage(CommunicatorBase.eMessageType.Instruction, jsonInstruction);
                    var response = await WaitForResponse();
                    _completed &= response.responded;
                    _sb.AppendLine(response.message);
                }
                else
                {
                    _sb.AppendLine($"Failed to resolve the '{instruction.action}' instruction!");
                }
            }

            if (instruction.instructions != null && instruction.instructions.Length > 0)
            {
                InsertAdditionalInstructions(i, new List<Instruction>(instruction.instructions));
            }
        }

        public async Awaitable<(bool succeeded, string response)> ProcessEnteredIntent(string intent)
        {
            _instructions = await _tanslation.Translate(intent);

            if (_instructions == null)
            {
                return (false, null);
            }

            _completed = true;
            _sb.Clear();

            for (int i = 0; i < _instructions.Count && _completed; ++i)
            {
                await ProcessInstruction(i);
            }

            return (_completed, _sb.ToString());
        }
    }
}