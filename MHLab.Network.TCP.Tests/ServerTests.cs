using System;
using System.Net;
using System.Threading;
using MHLab.Network.TCP.Server;
using Xunit;

namespace MHLab.Network.TCP.Tests
{
    public class ServerTests
    {
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
            
            var server = new Server.Server(7200, null, OnConnection, null);
            server.Start();

            var client = Client.Create(null, null, null);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));

            _waiter.Wait();
            Assert.True(_connected);
        }
        
        [Fact]
        public void Disconnection()
        {
            ResetState();
            
            var server = new Server.Server(7200, null, null, OnDisconnection);
            server.Start();

            var client = Client.Create(null, null, null);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));
            
            client.Disconnect();

            _waiter.Wait();
            Assert.True(_disconnected);
        }
        
        [Fact]
        public void Data()
        {
            ResetState();
            
            var server = new Server.Server(7200, OnData, null, null);
            server.Start();

            var client = Client.Create(null, null, null);
            client.Connect(new IPEndPoint(IPAddress.Loopback, 7200));

            var data = new byte[5] {85, 29, 33, 12, 196};
            client.SendData(data);

            _waiter.Wait();
            Assert.True(_received);
            Assert.NotNull(_receivedData);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.Equal(data[i], _receivedData[i]);
            }
        }

        private void OnDisconnection(Connection obj)
        {
            _disconnected = true;
            _waiter.Set();
        }

        private void OnConnection(Connection obj)
        {
            _connected = true;
            _waiter.Set();
        }

        private void OnData(Connection arg1, byte[] arg2)
        {
            _received = true;
            _receivedData = arg2;
            _waiter.Set();
        }
    }
}