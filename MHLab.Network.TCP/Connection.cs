using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace MHLab.Network.TCP.Server
{
    public class Connection
    {
        private Socket _connection;
        private AsyncCallback _readHeaderCallback;
        private AsyncCallback _readBodyCallback;
        private Action<Connection, byte[]> _onDataCallback;
        private Action<Connection> _onDisconnectionCallback;
        
        public static Connection Create(Socket connection, Action<Connection, byte[]> onData, Action<Connection> onDisconnection)
        {
            return new Connection(connection, onData, onDisconnection);
        }
        
        public static void Release(Connection connection)
        {
            
        }

        private Connection(Socket connection, Action<Connection, byte[]> onData, Action<Connection> onDisconnection)
        {
            _readHeaderCallback = ReadHeaderCallback;
            _readBodyCallback = ReadBodyCallback;
            _onDataCallback = onData;
            _onDisconnectionCallback = onDisconnection;
            _connection = connection;
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
                
                _onDataCallback?.Invoke(this, buffer.Buffer);
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
                _onDisconnectionCallback?.Invoke(this);
                Release(this);
            }
        }
    }
}