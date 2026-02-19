using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;

namespace T2G
{
    public class CommunicatorBase
    {
        public enum eMessageType : byte
        {
            Void = 0,
            Message,                  //A pain text message
            Settings,                 //the SettingsT2G json data
            Instruction,              //An instruction
            Response                  //A response with a data { "result": true, "message":"bla! bla! bla!" }
        }

        public struct MessageStruct
        {
            public eMessageType Type;
            public FixedString4096Bytes Message;
        }

        public const int MAX_MESSAGE_LENGTH = 4096;
        public const int SEND_POOL_SIZE = 8;
        public const int RECEIVE_BUFFER_SIZE = 64;
        public const float TIME_OUT = 3.0f;

        public string IPAddress = "127.0.0.1";
        public ushort Port = 7778;

        public Action<string> OnSentMessage;
        public Action<eMessageType, string> OnReceivedMessage;
        public Action<string> OnError;

        protected NetworkPipeline _networkpipeline;
        protected NetworkSettings _networkSettings;
        protected NetworkDriver _networkDriver;
        protected NativeArray<NetworkConnection> _connection;
        protected JobHandle _jobHandle;

        protected NativeArray<MessageStruct> _sendPool;
        protected int _sendPoolHead = 0;
        protected int _sendPoolTail = 0;
        protected NativeArray<MessageStruct> _receiveBuffer;
        protected int _receiveBufferHead = 0;
        protected int _receiveBufferTail = 0;

        protected virtual void Init()
        {
            _networkSettings = new NetworkSettings();
            _networkSettings.WithNetworkConfigParameters();         //Use default
            _networkSettings.WithNetworkSimulatorParameters();      //Use default
            _networkDriver = NetworkDriver.Create(_networkSettings);
            _networkpipeline = _networkDriver.CreatePipeline(
                        typeof(FragmentationPipelineStage),
                        typeof(ReliableSequencedPipelineStage));

            _connection = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
            _sendPool = new NativeArray<MessageStruct>(SEND_POOL_SIZE, Allocator.Persistent);
            _receiveBuffer = new NativeArray<MessageStruct>(RECEIVE_BUFFER_SIZE, Allocator.Persistent);
            _sendPoolHead = _sendPoolTail = _receiveBufferHead = _receiveBufferTail = 0;
        }

        public virtual void Dispose()
        {
            if(!IsActive)
            {
                return;
            }

            _jobHandle.Complete();

            if (IsConnected)
            {
                _sendPoolHead = _sendPoolTail = _receiveBufferHead = _receiveBufferTail = 0;
                _sendPool.Dispose();
                _receiveBuffer.Dispose();
                _connection[0].Close(_networkDriver);
                _connection.Dispose();
            }
            _networkDriver.ScheduleUpdate().Complete();
            _networkDriver.Dispose();
        }

        public virtual bool IsActive => false;
        public virtual bool IsConnected => (IsActive && _connection != null && _connection[0] != null && _connection[0].IsCreated);
        public bool IsSendPoolEmpty => (_sendPoolHead == _sendPoolTail);
        public bool IsReceiveBufferEmpty => (_receiveBufferHead == _receiveBufferTail);
        public bool IsReceiveBufferFull => (
            (_receiveBufferTail > 0 && _receiveBufferHead == _receiveBufferTail - 1)
            || (_receiveBufferTail == 0 && _receiveBufferHead == _receiveBuffer.Length - 1));

        private bool SendMessage(MessageStruct messageData)
        {
            if (_sendPoolTail == 0 && _sendPoolHead == _sendPool.Length - 1 ||
                _sendPoolTail > 0 && _sendPoolHead == _sendPoolTail - 1)
            {
                return false;       //The pool is full
            }

            _sendPool[_sendPoolHead++] = messageData;

            if (_sendPoolHead == _sendPool.Length)
            {
                _sendPoolHead = 0;
            }
            return true;
        }

        virtual public bool SendMessage(eMessageType type, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }
            MessageStruct msg = new MessageStruct { Type = type, Message = message };
            SendMessage(msg);
            return true;
        }

        protected bool SendPooledMessege()
        {
            if (IsConnected &&
                PopPooledMessage(out var messageData) && 
                messageData.Type != eMessageType.Void &&
                messageData.Message.Length <= MAX_MESSAGE_LENGTH)
            {
                _networkDriver.BeginSend(_networkpipeline, _connection[0], out var writer);
                writer.WriteByte((byte)messageData.Type);
                writer.WriteFixedString4096(messageData.Message);
                _networkDriver.EndSend(writer);
                OnSentMessage?.Invoke(messageData.Message.ToString());
                return true;
            }
            return false;
        }

        protected bool PopPooledMessage(out MessageStruct messageData)
        {
            if (_sendPoolHead != _sendPoolTail)
            {
                messageData = _sendPool[_sendPoolTail++];
                if (_sendPoolTail >= SEND_POOL_SIZE)
                {
                    _sendPoolTail = 0;
                }
                return true;
            }
            messageData = default(MessageStruct);
            return false;
        }


        protected bool AddReceivedMessageToBuffer(eMessageType type, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }
            MessageStruct msg = new MessageStruct
            {
                Type = type,
                Message = message
            };
            return AddReceivedMessageToBuffer(msg);
        }

        protected bool AddReceivedMessageToBuffer(MessageStruct messageData)
        {
            if (IsReceiveBufferFull)
            {
                OnError?.Invoke("The receiving buffer is full!");
                return false;
            }
            _receiveBuffer[_receiveBufferHead++] = messageData;
            if (_receiveBufferHead == _receiveBuffer.Length)
            {
                _receiveBufferHead = 0;
            }
            OnReceivedMessage?.Invoke(messageData.Type, messageData.Message.ToString());
            return true;
        }

        protected bool AddReceivedMessageToBuffer(ref NativeArray<MessageStruct> buffer, MessageStruct messageData)
        {
            if (IsReceiveBufferFull)
            {
                OnError?.Invoke("The receiving buffer is full!");
                return false;
            }
            buffer[_receiveBufferHead++] = messageData;
            if (_receiveBufferHead == _receiveBuffer.Length)
            {
                _receiveBufferHead = 0;
            }
            OnReceivedMessage?.Invoke(messageData.Type, messageData.Message.ToString());
            return true;
        }

        public bool RetriveMessageFromReceiveBuffer(out MessageStruct messageData)
        {
            if (IsReceiveBufferEmpty)
            {
                messageData = default;
                return false;
            }
            messageData = _receiveBuffer[_receiveBufferTail++];
            if (_receiveBufferTail == _receiveBuffer.Length)
            {
                _receiveBufferTail = 0;
            }
            return true;
        }

        public void EmptyReceiveBuffer()
        {
            _receiveBufferHead = _receiveBufferTail = 0;
        }
    }
}