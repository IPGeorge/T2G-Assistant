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


        [Header("UI Settings")]
        [SerializeField] private Settings _settings;
        public Settings Settings => _settings;
        public string PersistentDataPath { get; private set; }

        private ChatBotUI _chatBot;
        private Translation _tanslation = Translation.Instance;
        private Resolution _resolution = Resolution.Instance;

        public CommunicatorClient Communicator { get; private set; }

        private List<InstructionBase> _instructions = new List<InstructionBase>();
        private Dictionary<string, string> _instructionLocalGlobalIdMap = new Dictionary<string, string>();
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
            CommunicatorClient.Instance.ShakeHand = false;
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
            CommunicatorClient.Instance.ShakeHand = false;
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
                CommunicatorClient.Instance.ShakeHand = true;
            }
        }

        private void OnError(string errorMesasge)
        {
            Debug.LogError($"[Assistant] Error: {errorMesasge}");
        }

 #endregion Communicator event handlers

        async Awaitable<Response> WaitForResponse()
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
                    return new Response(false, "Timeout waiting for the response!");
                }
            }
            Communicator.RetriveMessageFromReceiveBuffer(out var responseData);
            return JsonConvert.DeserializeObject<Response>(responseData.Message.ToString());
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
            var instructionBase = _instructions[i];

            if (instructionBase.type == InstructionType.Sequence)
            {
                InstructionSequence instructionSequence = instructionBase as InstructionSequence;
                ExponentialBackoffFocusRestorer.NeedFocus = true;
                _sb.AppendLine($"Start batching {instructionSequence.instructions.Count} instructions:");
                _completed = true;
            }
            else
            {
                var instruction = instructionBase as Instruction;
                if (instruction.type == InstructionType.Local)
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
                    if (instruction.state == InstructionState.Raw)
                    {
                        instruction = await _resolution.Resolve(instruction);  //The returned instruction must be either raw or resolved
                    }

                    ExponentialBackoffFocusRestorer.NeedFocus = true;

                    if (instruction.state == InstructionState.Resolved)
                    {
                        Communicator.EmptyReceiveBuffer();
                        string jsonInstruction = JsonConvert.SerializeObject(instruction);
                        Communicator.SendMessage(CommunicatorBase.eMessageType.Instruction, jsonInstruction);
                        var response = await WaitForResponse();

                        if (response.Succeeded)
                        {
                            int paramIndex = response.Message.IndexOf("\n");

                            if (paramIndex >= 0)
                            {
                                string[] responseParams = response.Message.Substring(paramIndex + 1).Split(';');
                                GameDescManager.RecordInstruction(instruction, response, responseParams);
                            }
                            else
                            {
                                GameDescManager.RecordInstruction(instruction, response, null);
                            }
                        }

                        _completed &= response.Succeeded;
                        _sb.AppendLine(response.Message);
                    }
                    else
                    {
                        _completed = false;
                        _sb.AppendLine($"Failed to resolve the '{instruction.action}' instruction!");
                    }
                }
            }

            return _completed;
        }

        public async Awaitable<(bool succeeded, string response)> ProcessEnteredIntent(string intent)
        {
            var translatedInstructions = await _tanslation.Translate(intent.Trim());

            _instructionLocalGlobalIdMap.Clear();
            _instructions.Clear();
            foreach (var translatedInstruction in translatedInstructions)
            {
                if (string.IsNullOrEmpty(translatedInstruction.id))
                {
                    _instructionLocalGlobalIdMap[translatedInstruction.id] = 
                        translatedInstruction.id = 
                        InstructionIdGenerator.Instance.NextId();
                }

                var normailizedInstruction = InstructionNormalizer.Normalize(translatedInstruction);
                var result = InstructionValidator.Validate(normailizedInstruction, _instructionLocalGlobalIdMap);
                if (result.IsValid)
                {
                    _instructions.Add(normailizedInstruction);
                }
                else
                {
                    _completed = false;
                    return (_completed, result.Errors.ToString());
                }
            }

            _completed = true;

            if (_instructions.Count == 0)
            {
                return (_completed, "No more instructions to execute!");
            }
            
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