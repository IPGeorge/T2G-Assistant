#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.EditorCoroutines.Editor;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEditor;
using UnityEngine;


namespace T2G
{
    public class CommunicatorServer : CommunicatorBase
    {
        const string BACKUP_FILE_NAME = "PooledServerMessages.json";

        public Action OnServerStarted;
        public Action OnFailedToStartServer;
        public Action BeforeDisconnectClient;
        public Action AfterDisconnectClient;
        public Action BeforeShutdownServer;
        public Action AfterShutdownServer;
        
        public Action OnClientConnected;
        public Action OnClientDisconnected;
        
        public Action<string> OnLogMessage;

        static CommunicatorServer _instance = null;
        public static CommunicatorServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CommunicatorServer();
                    _instance.Init();

                }
                return _instance;
            }
        }

        ~CommunicatorServer()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_connection != null && _connection[0] != null &&_connection[0].IsCreated)
            {
                BeforeDisconnectClient?.Invoke();
                _networkDriver.Disconnect(_connection[0]);
                _connection[0] = default(NetworkConnection);
                _connection.Dispose();
                AfterDisconnectClient?.Invoke();
            }
            base.Dispose();
        }

        public override bool IsActive => (_networkDriver.IsCreated && _networkDriver.Listening);

        public void StartServer()
        {
            if(IsConnected)
            {
                return;
            }

            if (!IsActive)
            {
                Init();
            }

            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(Port);
            if (_networkDriver.Bind(endpoint) == 0)
            {
                _networkDriver.Listen();
                OnServerStarted?.Invoke();
            }
            else
            {
                Dispose();
                OnFailedToStartServer?.Invoke();
            }

            EditorApplication.update += UpdateServer;

            EditorCoroutineUtility.StartCoroutine(RestorePooledMessages(), this);
        }

        public void StopServer()
        {
            BeforeShutdownServer?.Invoke();

            EditorApplication.update -= UpdateServer;

            if (_connection != null && _connection[0] != null && _connection[0].IsCreated)
            {
                BeforeDisconnectClient?.Invoke();
                _networkDriver.Disconnect(_connection[0]);
                _connection[0] = default(NetworkConnection);
                AfterDisconnectClient?.Invoke();
            }

            base.Dispose();
            AfterShutdownServer?.Invoke();
        }

        void UpdateServer()
        {
            if (!IsActive)
            {
                return;
            }

            _jobHandle = _networkDriver.ScheduleUpdate();

            if (!IsConnected)
            {
                var connectionJob = new ServerConnectionJob()
                {
                    Driver = _networkDriver,
                    Connection = _connection
                };

                _jobHandle = connectionJob.Schedule(_jobHandle);
                _jobHandle.Complete();
                if (IsConnected)
                {
                    OnClientConnected?.Invoke();
                }
            }

            if (IsConnected)
            {
                var receiveJob = new ServerReceiveJob()
                {
                    Driver = _networkDriver,
                    Connection = _connection,
                    ReceiveBuffer = _receiveBuffer,
                    SendPool = _sendPool
                };
                _jobHandle = receiveJob.Schedule(_jobHandle);
                _jobHandle.Complete();

                SendPooledMessege();
            }
        }

        struct ServerConnectionJob : IJob
        {
            public NetworkDriver Driver;
            public NativeArray<NetworkConnection> Connection;

            public void Execute()
            {
                NetworkConnection newConnection;
                while ((newConnection = Driver.Accept()) != default)
                {
                    Connection[0] = newConnection;
                }
            }
        }

        struct ServerReceiveJob : IJob
        {
            public NetworkDriver Driver;
            public NativeArray<NetworkConnection> Connection;
            public NativeArray<MessageStruct> ReceiveBuffer;
            public NativeArray<MessageStruct> SendPool;

            public void Execute()
            {
                if (!Connection.IsCreated)
                {
                    return;
                }

                DataStreamReader readStream;
                NetworkEvent.Type command;
                var communicator = CommunicatorServer.Instance;
                while ((command = Connection[0].PopEvent(Driver, out readStream)) != NetworkEvent.Type.Empty)
                {
                    if (command == NetworkEvent.Type.Data)
                    {
                        var receivedData = new MessageStruct()
                        {
                            Type = (eMessageType)readStream.ReadByte(),
                            Message = readStream.ReadFixedString4096()
                        };

                        communicator.AddReceivedMessageToBuffer(ref ReceiveBuffer, receivedData);
                    }
                    else if (command == NetworkEvent.Type.Disconnect)
                    {
                        Connection[0] = default;
                        communicator.OnClientDisconnected?.Invoke();
                    }
                }
            }
        }

        private IEnumerator RestorePooledMessages()
        {
            while (AssetPostProcessHandler.IsProcessingAssets)
            {
                yield return new WaitForSeconds(0.5f);
            }
            CommunicatorServer.Instance.RestoreSendingAndReceivingPools();
            Debug.Log("[CommunicatorServer.RestorePooledMessages] Pooled messages were succedssfully restored!");
        }

        void SaveSendingAndReceivingPools()
        {
            List<int> messageTypes = new List<int>();
            List<string> messages = new List<string>();
            BackupPoolsData data = new BackupPoolsData();

            while (PopPooledMessage(out var message))
            {
                messageTypes.Add((int)message.Type);
                messages.Add(message.Message.ToString());
            }
            data.SendingMessageTypes = messageTypes.ToArray();
            data.SendingMessages = messages.ToArray();

            messageTypes.Clear();
            messages.Clear();
            while (RetriveMessageFromReceiveBuffer(out var message))
            {
                messageTypes.Add((int)message.Type);
                messages.Add(message.Message.ToString());
            }
            data.ReceivingMessageTypes = messageTypes.ToArray();
            data.ReceivingMessages = messages.ToArray();

            string json = EditorJsonUtility.ToJson(data);
            string path = Path.Combine(Application.persistentDataPath, BACKUP_FILE_NAME);
            File.WriteAllText(path, json);
        }

        void RestoreSendingAndReceivingPools()
        {
            int i;
            string path = Path.Combine(Application.persistentDataPath, BACKUP_FILE_NAME);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                File.Delete(path);
                try
                {
                    var data = JsonUtility.FromJson<BackupPoolsData>(json);
                    for (i = 0; i < data.SendingMessageTypes.Length; ++i)
                    {
                        SendMessage((eMessageType)data.SendingMessageTypes[i], data.SendingMessages[i]);
                    }

                    for (i = 0; i < data.ReceivingMessageTypes.Length; ++i)
                    {
                        AddReceivedMessageToBuffer((eMessageType)data.SendingMessageTypes[i], data.SendingMessages[i]);
                    }
                }
                catch (Exception e)
                {
                    string errMsg = $"[CommunicatorServer.RestoreSendingAndReceivingPools] Pooled messages data file was corrupted!\n{e.Message}";
                    Debug.LogError(errMsg);
                    OnError?.Invoke(errMsg);
                }
            }
        }
    }

    [Serializable] class BackupPoolsData
    {
        public int[] SendingMessageTypes;
        public string[] SendingMessages;
        public int[] ReceivingMessageTypes;
        public string[] ReceivingMessages;
    }


    public class AssetPostProcessHandler : AssetPostprocessor
    {
        public static bool IsProcessingAssets = true;

        //This is called after importing of any number of assets is complete
        //(when the Assets progress bar has reached the end).
        protected static void OnPostprocessAllAssets(string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            IsProcessingAssets = false;
        }
    }
}

#endif