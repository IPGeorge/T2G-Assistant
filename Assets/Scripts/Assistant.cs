using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    public partial class Assistant : MonoBehaviour
    {
        static public Assistant Instance { get; private set; } = null;

        public bool ShakeHandk { get; private set; } = false;

        [Header("UI Settings")]
        [SerializeField] private Settings _settings;
        public Settings Settings => _settings;
        public string PersistentDataPath { get; private set; }

        private ChatBotUI _chatBot;
        private Translation _tanslation = Translation.Instance;
        private Resolution _resolution = Resolution.Instance;

        public CommunicatorClient Communicator { get; private set; }

        private List<Instruction> _instructions;
        private bool _completed;
        private StringBuilder _sb = new StringBuilder();

        public ProjectContext GameProject { get; private set; } = null;
        public GameDescManager GameDescManager { get; private set; } = null;

        private void Awake()
        {
            PersistentDataPath = Application.persistentDataPath;
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _chatBot = ChatBotUI.Instance;
            GameDescManager = GameDescManager.Instance;
            Init();
        }

        private async void Update()
        {
            if(ExponentialBackoffFocusRestorer.NeedFocus && 
                !ExponentialBackoffFocusRestorer.IsRestoringFocus &&
                !ExponentialBackoffFocusRestorer.IsFocusedWindow())
            {
                await ExponentialBackoffFocusRestorer.RestoreFocusWithExponentialBackoff();
            }

            Communicator?.UpdateClient();
        }

        private void OnDestroy()
        {
            if(Communicator != null && Communicator.IsConnected)
            {
                Communicator.Disconnect();
            }

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
            ShakeHandk = false;
            ExponentialBackoffFocusRestorer.Initialize();
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
            ShakeHandk = false;
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

        private void OnSentMessage(CommunicatorBase.eMessageType type, string message)
        {
            Debug.Log($"[Assistant] Sent message: {message}");
        }

        private void OnReceivedMessage(CommunicatorBase.eMessageType type, string message)
        {
            if(type == CommunicatorBase.eMessageType.ProjectInfo)
            {
                Debug.Log($"Received project info: {message}");
                var pi = JsonConvert.DeserializeObject<ProjectInfo>(message);
                if(pi != null && !string.IsNullOrEmpty(pi.ProjectName))
                {
                    if(GameDescManager.Snapshot != null && 
                        !string.IsNullOrEmpty(GameDescManager.CurrentProjectName))
                    {
                        GameDescManager.SaveGameDesc();
                    }
                    OpenOrCreateProjectContext(pi);
                }
                Communicator.RemoveMessageFromReceiveBuffer();
                ShakeHandk = true;
            }
        }

        private void OnError(string errorMesasge)
        {
            Debug.LogError($"[Assistant] Error: {errorMesasge}");
        }

 #endregion Communicator event handlers

        async Awaitable<(bool responded, string message)> WaitForResponse()
        {
            int timeoutMiniSeconds = 60000;
            int waitInterval = 100;
            while(Communicator.IsReceiveBufferEmpty ||
                Communicator.GetNextReceivedMessageType() != CommunicatorBase.eMessageType.Response)
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

        async Awaitable<bool> ProcessInstruction(int i)
        {
            var instruction = _instructions[i];

            if (instruction.type == Instruction.k_TypeInstructionList)
            {
                ExponentialBackoffFocusRestorer.NeedFocus = true;
                _sb.AppendLine($"Start batching {instruction.instructions.Length} instructions:");
                _completed = true;
            }
            else if(instruction.type == Instruction.k_TypeFailed)
            {
                _sb.AppendLine("Sorry, I don't know how to accomplish this task!");
                _completed = false;
            }
            else if(instruction.type == Instruction.k_TypeQuestion)
            {
                //Support it later
            }
            else
            {
                if (instruction.state == Instruction.eState.Local)
                {
                    var result = await LocalExecution.Instance.Execute(instruction);
                    _completed = result.succeeded;

                    if (!string.IsNullOrWhiteSpace(result.message))
                    {
                        _sb.AppendLine(result.message);
                    }

                    if (result.additionalInstructions != null && result.additionalInstructions.Count > 0)
                    {
                        InsertAdditionalInstructions(i, result.additionalInstructions);
                    }
                }
                else
                {
                    if (instruction.state == Instruction.eState.Raw)
                    {
                        instruction = await _resolution.Resolve(instruction);  //The returned instruction must be either raw or resolved
                    }

                    ExponentialBackoffFocusRestorer.NeedFocus = true;

                    if (instruction.state == Instruction.eState.Resolved)
                    {
                        Communicator.EmptyReceiveBuffer();
                        string jsonInstruction = JsonConvert.SerializeObject(instruction);
                        Communicator.SendMessage(CommunicatorBase.eMessageType.Instruction, jsonInstruction);
                        var response = await WaitForResponse();

                        if (response.responded)
                        {
                            int paramIndex = response.message.IndexOf("\n");

                            if (paramIndex >= 0)
                            {
                                string[] responseParams = response.message.Substring(paramIndex + 1).Split(';');
                                GameDescManager.RecordInstruction(instruction, new Response(response.responded, response.message), responseParams);
                            }
                            else
                            {
                                GameDescManager.RecordInstruction(instruction, new Response(response.responded, response.message), null);
                            }
                        }

                        _completed &= response.responded;
                        _sb.AppendLine(response.message);
                    }
                    else
                    {
                        _completed = false;
                        _sb.AppendLine($"Failed to resolve the '{instruction.action}' instruction!");
                    }
                }
            }

            if (instruction.instructions != null && instruction.instructions.Length > 0)
            {
                InsertAdditionalInstructions(i, new List<Instruction>(instruction.instructions));
            }

            return _completed;
        }

        public async Awaitable<(bool succeeded, string response)> ProcessEnteredIntent(string intent)
        {
            _instructions = await _tanslation.Translate(intent.Trim());

            if (_instructions == null)
            {
                return (false, null);
            }

            _completed = true;
            _sb.Clear();

            for (int i = 0; i < _instructions.Count && _completed; ++i)
            {
                _completed &= await ProcessInstruction(i);
                _chatBot?.UpdateLastBotMessage(_sb.ToString());
                await Task.Delay(100);
            }

            TranslationLogger.Append(new TranslationRecord()
            {
                prompt = intent,
                success = _completed,
                instructionList = _instructions
            }); 

            return (_completed, _sb.ToString());
        }

        public void SaveCurrentProject()
        {
            if(GameProject != null)
            {
                GameProject.Save();
                GameDescManager.SaveGameDesc(GameProject.ProjectName);
                Debug.Log($"Current GameProject {GameProject} is saved!");
            }
            else
            {
                Debug.Log("Current GameProject is null!");
            }
        }

        public void CreateNewProjectContext(string projectName, string projectPath)
        {
            SaveCurrentProject();
            GameProject = new ProjectContext() { ProjectName = projectName, ProjectPath = projectPath, Genre="", Engine="Unity" };
            GameDescManager.CreateGameDescProject(projectName);
            SaveCurrentProject();
        }

        public bool OpenOrCreateProjectContext(ProjectInfo projectInfo)
        {
            if(string.IsNullOrEmpty(projectInfo.ProjectName))
            {
                return false;
            }
             
            SaveCurrentProject();
            GameProject = ProjectContext.LoadOrCreate(projectInfo);
            GameProject.CurrentSpace = projectInfo.CurrentSpace;

            if (GameProject == null || string.IsNullOrWhiteSpace(GameProject.ProjectName))
            {
                return false;
            }
            GameDescManager.OpenOrCreateGameDesc(projectInfo.ProjectName, projectInfo.Title);
            return true;
        }

        public int FindSavedProjectIndex(string projectName)
        {
            var prjList = ProjectContext.GetProjectList();
            return prjList.FindIndex(item => string.Equals(item, projectName, StringComparison.OrdinalIgnoreCase));
        }
    }
}