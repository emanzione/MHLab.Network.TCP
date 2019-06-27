using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MHLab.Network.TCP.Server;

namespace MHLab.Network.TCP
{
    public class Client
    {
        private Socket _connection;
        private AsyncCallback _readHeaderCallback;
        private AsyncCallback _readBodyCallback;
        private Action<byte[]> _onDataCallback;
        private Action _onDisconnectionCallback;
        private Action _onConnectionCallback;
        
        public static Client Create(Action<byte[]> onData, Action onConnection, Action onDisconnection)
        {
            return new Client(onData, onConnection, onDisconnection);
        }
        
        public static void Release(Client connection)
        {
            
        }

        private Client(Action<byte[]> onData, Action onConnection, Action onDisconnection)
        {
            _readHeaderCallback = ReadHeaderCallback;
            _readBodyCallback = ReadBodyCallback;
            _onDataCallback = onData;
            _onDisconnectionCallback = onDisconnection;
            _onConnectionCallback = onConnection;
        }

        public void Connect(IPEndPoint endpoint)
        {
            if (endpoint.AddressFamily == AddressFamily.InterNetwork)
            {
                _connection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new Exception("IPv6 is not supported on this OS.");

                _connection = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                _connection.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }

            _connection.Blocking = false;
            _connection.DontFragment = true;
            _connection.NoDelay = true;
            _connection.ReceiveBufferSize = Settings.BufferSize;
            _connection.SendBufferSize = Settings.BufferSize;

            var connectionResult = _connection.BeginConnect(endpoint, (result) =>
            {
                StartReceiving();
                _onConnectionCallback?.Invoke();
            }, null);
            
            _connection.EndConnect(connectionResult);
        }

        public void Disconnect()
        {
            HandleDisconnection();
        }

        public void SendData(byte[] data)
        {
            if (_connection.Connected)
            {
                var preparedPacket = AppendLengthHeader(data);
                _connection.BeginSend(preparedPacket, 0, preparedPacket.Length, SocketFlags.None, null, null);
            }
        }
        
        private byte[] AppendLengthHeader(byte[] bytes)
        {
            byte[] fullBytes = new byte[bytes.Length + 4];

            //Append length
            fullBytes[0] = (byte)(((uint)bytes.Length >> 24) & 0xFF);
            fullBytes[1] = (byte)(((uint)bytes.Length >> 16) & 0xFF);
            fullBytes[2] = (byte)(((uint)bytes.Length >> 8) & 0xFF);
            fullBytes[3] = (byte)(uint)bytes.Length;

            //Add rest of bytes
            Buffer.BlockCopy(bytes, 0, fullBytes, 4, bytes.Length);

            return fullBytes;
        }

        public void StartReceiving()
        {
            ReceiveHeader();
        }

        private void ReceiveHeader()
        {
            var buffer = ReceiveBuffer.Create(4);
            lock (_connection)
            {
                _connection.BeginReceive(buffer.Buffer, buffer.ReceivedBytes, buffer.Buffer.Length, SocketFlags.None,
                    _readHeaderCallback, buffer);
            }
        }

        private void ReceiveHeader(ReceiveBuffer buffer)
        {
            lock (_connection)
            {
                _connection.BeginReceive(buffer.Buffer, buffer.ReceivedBytes, buffer.Buffer.Length, SocketFlags.None,
                    _readHeaderCallback, buffer);
            }
        }

        private void ReadHeaderCallback(IAsyncResult result)
        {
            int receivedBytes = 0;
            try
            {
                lock (_connection)
                {
                    receivedBytes = _connection.EndReceive(result);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                HandleDisconnection();
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                return;
            }

            if (receivedBytes == 0)
            {
                HandleDisconnection();
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                return;
            }
            
            var buffer = (ReceiveBuffer)result.AsyncState;
            buffer.ReceivedBytes += receivedBytes;

            if (buffer.ReceivedBytes < buffer.Buffer.Length)
            {
                try
                {
                    ReceiveHeader(buffer);
                }
                catch (Exception)
                {
                    HandleDisconnection();
                    ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                    return;
                }
            }
            else
            {
                Debug.Assert(buffer.ReceivedBytes == 4);
                var bytes = buffer.Buffer;
                int length = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                ReceiveBody(length);
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
            }
        }

        private void ReceiveBody(int size)
        {
            var buffer = ReceiveBuffer.Create(size);
            lock (_connection)
            {
                _connection.BeginReceive(buffer.Buffer, buffer.ReceivedBytes, buffer.Buffer.Length, SocketFlags.None,
                    _readBodyCallback, buffer);
            }
        }

        private void ReceiveBody(ReceiveBuffer buffer)
        {
            lock (_connection)
            {
                _connection.BeginReceive(buffer.Buffer, buffer.ReceivedBytes, buffer.Buffer.Length, SocketFlags.None,
                    _readBodyCallback, buffer);
            }
        }

        private void ReadBodyCallback(IAsyncResult result)
        {
            int receivedBytes = 0;
            try
            {
                lock (_connection)
                {
                    receivedBytes = _connection.EndReceive(result);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                HandleDisconnection();
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                return;
            }

            if (receivedBytes == 0)
            {
                HandleDisconnection();
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                return;
            }
            
            var buffer = (ReceiveBuffer)result.AsyncState;
            buffer.ReceivedBytes += receivedBytes;

            if (buffer.ReceivedBytes < buffer.Buffer.Length)
            {
                try
                {
                    ReceiveBody(buffer);
                }
                catch (Exception)
                {
                    HandleDisconnection();
                    ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
                    return;
                }
            }
            else
            {
                ReceiveHeader();
                
                _onDataCallback?.Invoke(buffer.Buffer);
                ReceiveBuffer.Release((ReceiveBuffer)result.AsyncState);
            }
        }
        
        private void HandleDisconnection()
        {
            lock (_connection)
            {
                if(_connection.Connected)
                    _connection.Shutdown(SocketShutdown.Send);
                _connection.Close();
                _onDisconnectionCallback?.Invoke();
                Release(this);
            }
        }
    }
}