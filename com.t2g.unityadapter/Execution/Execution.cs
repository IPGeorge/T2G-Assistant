#if UNITY_EDITOR

using UnityEngine;
using T2G;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using UnityEngine.SceneManagement;

namespace T2G
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ExecutorAttribute : Attribute
    {
        public string Action { get; private set; }

        public ExecutorAttribute(string instructionAction)
        {
            Action = instructionAction;
        }
    }

    public class Execution
    {
        public Action<string> OnDisplayText = null;
        
        public SettingsLite Settings { get; private set; } = new SettingsLite();
        CommunicatorServer _server;

        private static Execution _instance = null;
        public static Execution Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Execution();
                    _instance.Init();
                }
                return _instance;
            }
        }

        public bool ShakeHand { get; private set; } = false;

        public Execution()
        {
            Register_Executors();
        }

        ~Execution()
        {
            Uninit();
        }

        private void Init()
        {
            _server = CommunicatorServer.Instance;
            ShakeHand = false;
            EditorApplication.update += Update;
        }

        private void Uninit()
        {
            EditorApplication.update -= Update;
            ShakeHand = false;
        }

        Dictionary<string, ExecutorBase> _executorMap = new Dictionary<string, ExecutorBase>();

        void Register_Executors()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var translatorClasses = assembly.GetTypes()
                .Where(type => type.IsClass && type.GetCustomAttributes(typeof(ExecutorAttribute), false).Any());
            foreach (var translatorClass in translatorClasses)
            {
                var attribute = translatorClass.GetCustomAttribute<ExecutorAttribute>();
                var executor = (ExecutorBase)(Activator.CreateInstance(translatorClass));

                _executorMap.Add(attribute.Action, executor);
            }
        }

        private async void Update()
        {
            if (_server.RetriveMessageFromReceiveBuffer(out var messageData))
            {
                switch (messageData.Type)
                {
                    case CommunicatorBase.eMessageType.Settings:
                        {
                            string settingsJson = messageData.Message.ToString();
                            Settings = JsonConvert.DeserializeObject<SettingsLite>(settingsJson);
                            Debug.Log("Received settings: " + settingsJson);

                            ProjectInfo pi = new ProjectInfo()
                            {
                                ProjectName = T2G.Utils.GetProjectName(),
                                ProjectPath = T2G.Utils.GetProjectPath(),
                                Title = Application.productName,
                                CurrentSpace = SceneManager.GetActiveScene().name
                            };
                            string piJson = JsonConvert.SerializeObject(pi);
                            _server.SendMessage(CommunicatorBase.eMessageType.ProjectInfo, piJson);
                            ShakeHand = true;
                        }
                        break;
                    case CommunicatorBase.eMessageType.Instruction:
                        {
                            Instruction instruction = JsonConvert.DeserializeObject<Instruction>(messageData.Message.ToString());
                            Response response = new Response();
                            bool isBusy = false;
                            if (_executorMap.ContainsKey(instruction.action))
                            {
                                var executor = _executorMap[instruction.action];
                                var result = await executor.Execute(instruction);
                                response.Succeeded = result.succeeded;
                                response.Message = result.message;
                                response.ObjectId = executor.ResultObjectId ?? string.Empty;
                                isBusy = string.IsNullOrEmpty(result.message);
                            }
                            else
                            {
                                response.Succeeded = false;
                                response.Message = "No appropriate exector was found!";
                            }

                            if (!isBusy)
                            {
                                SendExecutionResponse(response);
                            }
                        }
                        break;
                    case CommunicatorBase.eMessageType.Message:
                        CommunicatorServer.Instance.SendMessage(CommunicatorServer.eMessageType.Message, $"Received message: {messageData.Message}");
                        break;
                    default:
                        break;
                }
            }
        }

        public async void SendExecutionResponse(Response response)
        {
            while(!CommunicatorServer.Instance.IsConnected)
            {
                await Task.Delay(100);
            }
            string responseJson = JsonConvert.SerializeObject(response);
            CommunicatorServer.Instance.SendMessage(CommunicatorBase.eMessageType.Response, responseJson);
        }

        public void SendExecutionResponse(bool succeded, string responseMessage)
        {
            Response response = new Response(succeded, responseMessage);
            SendExecutionResponse(response);
        }
    }
}
#endif