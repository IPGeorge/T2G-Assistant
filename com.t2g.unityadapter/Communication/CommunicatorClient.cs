using UnityEngine;

using System;
using Unity.Networking.Transport;
using Unity.Jobs;
using Unity.Collections;
using System.Threading.Tasks;

namespace T2G
{
    public class CommunicatorClient : CommunicatorBase
    {
        public Action OnConnectedToServer;
        public Action OnFailedToConnectToServer;
        public Action OnDisconnecting;
        public Action OnDisconnected;

        public NetworkConnection.State ConnectionState => _networkDriver.GetConnectionState(_connection[0]);
        public override bool IsConnected => (ConnectionState == NetworkConnection.State.Connected);

        static CommunicatorClient _instance = null;
        public static CommunicatorClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CommunicatorClient();
                    _instance.Init();
                }
                return _instance;
            }
        }

        public async Awaitable StartClient()
        {
            if (_networkDriver.IsCreated && ConnectionState != NetworkConnection.State.Disconnected)
            {
                return;
            }

            if (IsActive)
            {
                Dispose();
            }

            Init();

            NetworkEndpoint endPoint;
            if (!NetworkEndpoint.TryParse(IPAddress, Port, out endPoint))
            {
                endPoint = NetworkEndpoint.LoopbackIpv4.WithPort(Port);
            }

            Debug.Log("[CommunicatorClient] Connecting ...");
            _connection[0] = _networkDriver.Connect(endPoint);
            while (ConnectionState == NetworkConnection.State.Connecting)
            {
                await Task.Delay(300);
            }

            if (ConnectionState == NetworkConnection.State.Connected)
            {
                OnConnectedToServer?.Invoke();
            }
            else
            {
                OnFailedToConnectToServer?.Invoke();
            }
        }

        public void Disconnect()
        {
            _jobHandle.Complete();
            if (ConnectionState != NetworkConnection.State.Disconnected)
            {
                OnDisconnecting?.Invoke();
                _connection[0].Disconnect(_networkDriver);
                _networkDriver.ScheduleUpdate().Complete();
                OnDisconnected?.Invoke();
            }
        }

        protected new async Awaitable<bool> SendPooledMessege()
        {
            if (!IsConnected)
            {
                await StartClient();
            }

            if (IsConnected)
            {
                return base.SendPooledMessege();

            }
            else
            {
                return false;
            }
        }

        public async void UpdateClient()
        {
            _jobHandle.Complete();

            await SendPooledMessege();

            if (_networkDriver.IsCreated)
            {
                var job = new ClientJob()
                {
                    Driver = _networkDriver,
                    Connection = _connection,
                    ReceiveBuffer = _receiveBuffer
                };

                _jobHandle = _networkDriver.ScheduleUpdate();
                _jobHandle = job.Schedule(_jobHandle);
                _jobHandle.Complete();
            }
        }

        struct ClientJob : IJob
        {
            public NetworkDriver Driver;
            public NativeArray<NetworkConnection> Connection;
            public NativeArray<MessageStruct> ReceiveBuffer;

            public void Execute()
            {
                if (!Connection.IsCreated)
                {
                    return;
                }

                DataStreamReader readStream;
                NetworkEvent.Type command;
                var communicator = CommunicatorClient.Instance;
                while ((command = Connection[0].PopEvent(Driver, out readStream)) != NetworkEvent.Type.Empty)
                {
                    if (command == NetworkEvent.Type.Data) //Received data
                    {
                        var receivedMessage = new MessageStruct()
                        {
                            Type = (eMessageType)readStream.ReadByte(),
                            Message = readStream.ReadFixedString4096()
                        };

                        communicator.AddReceivedMessageToBuffer(ref ReceiveBuffer, receivedMessage);
                    }
                    else if (command == NetworkEvent.Type.Disconnect) //Disconnected
                    {
                        communicator.Dispose();
                        communicator.OnDisconnected?.Invoke();
                    }
                }
            }
        }
    }
}