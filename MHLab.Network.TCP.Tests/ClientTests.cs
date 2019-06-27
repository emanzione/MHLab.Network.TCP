using System;
using System.Net;
using System.Threading;
using MHLab.Network.TCP.Server;
using Xunit;

namespace MHLab.Network.TCP.Tests
{
    public class ClientTests
    {
        private byte[] _dataToSend = new byte[5] {85, 29, 33, 12, 196};
        private bool _connected = false;
        private bool _disconnected = false;
        private bool _received = false;
        private byte[] _receivedData;
        private ManualResetEventSlim _waiter = new ManualResetEventSlim(false);

        private void ResetState()
        {
            _connected = false;
            _disconnected = false;
            _received = false;
            _receivedData = null;
            _waiter.Reset();
        }
        
        [Fact]
        public void Connection()
        {
            ResetState();
            
            var server = new Server.Server(7200, null, null, null);
            server.Start();

            var client = Client.Create(null, OnConnection, null);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));

            _waiter.Wait();
            Assert.True(_connected);
        }
        
        [Fact]
        public void Disconnection()
        {
            ResetState();
            
            var server = new Server.Server(7200, null, null, null);
            server.Start();

            var client = Client.Create(null, null, OnDisconnection);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));
            
            client.Disconnect();

            _waiter.Wait();
            Assert.True(_disconnected);
        }
        
        [Fact]
        public void Data()
        {
            ResetState();
            
            var server = new Server.Server(7200, null, OnServerConnection, null);
            server.Start();

            var client = Client.Create(OnData, null, null);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));

            _waiter.Wait();
            Assert.True(_received);
            Assert.NotNull(_receivedData);
            for (int i = 0; i < _dataToSend.Length; i++)
            {
                Assert.Equal(_dataToSend[i], _receivedData[i]);
            }
        }

        private void OnDisconnection()
        {
            _disconnected = true;
            _waiter.Set();
        }

        private void OnConnection()
        {
            _connected = true;
            _waiter.Set();
        }

        private void OnServerConnection(Connection connection)
        {
            connection.SendData(_dataToSend);
        }
        
        private void OnData(byte[] arg2)
        {
            _received = true;
            _receivedData = arg2;
            _waiter.Set();
        }
    }
}