using System;
using System.Net;
using System.Net.Sockets;

namespace MHLab.Network.TCP.Server
{
    public class Server
    {
        private readonly IPEndPoint _listeningEndpoint;
        private readonly Socket _listeningSocket;

        private readonly AsyncCallback _handleAcceptCallback;
        private Action<Connection, byte[]> _onData;
        private Action<Connection> _onConnection;
        private Action<Connection> _onDisconnection;
        private Action<Connection, byte[]> _onCustomData;
        private Action<Connection> _onCustomConnection;
        private Action<Connection> _onCustomDisconnection;
        
        public Server(int port, Action<Connection, byte[]> onData, Action<Connection> onConnection, Action<Connection> onDisconnection)
        {
            _handleAcceptCallback = HandleAccept;
            
            _onData = OnData;
            _onConnection = OnConnection;
            _onDisconnection = OnDisconnection;

            _onCustomData = onData;
            _onCustomConnection = onConnection;
            _onCustomDisconnection = onDisconnection;
            
            _listeningEndpoint = new IPEndPoint(IPAddress.Any, port);

            if (_listeningEndpoint.AddressFamily == AddressFamily.InterNetwork)
            {
                _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new Exception("IPv6 is not supported on this OS.");

                _listeningSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                _listeningSocket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }

            _listeningSocket.Blocking = false;
            _listeningSocket.DontFragment = true;
            _listeningSocket.NoDelay = true;
            _listeningSocket.ReceiveBufferSize = Settings.BufferSize;
            _listeningSocket.SendBufferSize = Settings.BufferSize;
        }

        public void Start()
        {
            try
            {
                if (_listeningSocket.IsBound) return;
                lock (_listeningSocket)
                {
                    if (_listeningSocket.IsBound) return;
                    
                    _listeningSocket.Bind(_listeningEndpoint);
                    _listeningSocket.Listen(1000);

                    _listeningSocket.BeginAccept(_handleAcceptCallback, null);
                }
            }
            catch (SocketException)
            {
                throw;
            }
        }

        public void Close()
        {
            lock (_listeningSocket)
            {
                if(_listeningSocket.IsBound)
                    _listeningSocket.Shutdown(SocketShutdown.Receive);
                _listeningSocket.Close(100);
            }
        }

        private void HandleAccept(IAsyncResult result)
        {
            Socket acceptedSocket;
            
            lock (_listeningSocket)
            {
                try
                {
                    acceptedSocket = _listeningSocket.EndAccept(result);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _listeningSocket.BeginAccept(_handleAcceptCallback, null);
            }
            
            acceptedSocket.Blocking = false;
            acceptedSocket.DontFragment = true;
            acceptedSocket.NoDelay = true;
            acceptedSocket.ReceiveBufferSize = Settings.BufferSize;
            acceptedSocket.SendBufferSize = Settings.BufferSize;

            var connection = Connection.Create(acceptedSocket, _onData, _onDisconnection);
            connection.StartReceiving();
            OnConnection(connection);
        }

        private void OnData(Connection connection, byte[] data)
        {
            _onCustomData?.Invoke(connection, data);
        }

        private void OnConnection(Connection connection)
        {
            _onCustomConnection?.Invoke(connection);
        }
        
        private void OnDisconnection(Connection connection)
        {
            _onCustomDisconnection?.Invoke(connection);
        }
    }
}