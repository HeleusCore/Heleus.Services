using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Messages;
using Heleus.Service;
using NetMQ;
using NetMQ.Sockets;

namespace Heleus.PushService
{
    public class PushClient : IDisposable, ILogger
	{
        static PushClient()
        {
            PushServiceMessage.RegisterPushServiceMessages();
        }

        static readonly Dictionary<string, PushClient> _clients = new Dictionary<string, PushClient>();

        public static PushClient GetPushClient(string serverAddress)
        {
            lock (_clients)
            {
                if (_clients.TryGetValue(serverAddress, out var reference))
                {
                    reference._refCount++;
                    Log.Trace($"RefCount increased to {reference._refCount} for {reference.ConnectionAddress}.", reference);
                    return reference;
                }

                var client = new PushClient(serverAddress);
                _clients[serverAddress] = client;
                return client;
            }
        }

        public string LogName => GetType().Name;
        public readonly string ConnectionAddress;

        NetMQPoller _poller = new NetMQPoller();
        PushSocket _socket = new PushSocket();
        int _refCount;

        readonly LazyLookupTable<long, IServiceRemoteRequest> _subscriptionRemoteRequests = new LazyLookupTable<long, IServiceRemoteRequest> { LifeSpan = TimeSpan.FromSeconds(15), Depth = 2 };

        PushClient(string serverAddress)
		{
            ConnectionAddress = serverAddress;
            _refCount = 1;

            Log.Info($"New connection to address {serverAddress} created.", this);

            _poller.Add(_socket);
            _socket.Connect(serverAddress);
            _poller.RunAsync();
        }

        public void SendRemoteMessage(PushServiceMessage message)
        {
            if (_poller == null || _socket == null)
                return;

            new Task(() =>
            {
                try
                {
                    var messageData = message.ToByteArray();
                    _socket.SendFrame(messageData);
                }
                catch (Exception ex)
                {
                    Log.HandleException(ex, this);
                }
            }).Start(_poller);
        }

        ~PushClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            lock (_clients)
            {
                _refCount--;

                Log.Trace($"RefCount decreased to {_refCount} for {ConnectionAddress}.", this);

                if (_refCount <= 0)
                {
                    Log.Trace($"RefCount decreased to 0 for {ConnectionAddress}, disposing.", this);
                    _clients.Remove(ConnectionAddress);
                    _subscriptionRemoteRequests.Clear();

                    _poller?.Stop();
                    _poller?.Dispose();
                    _socket?.Dispose();

                    _poller = null;
                    _socket = null;
                }
            }
        }
    }
}
