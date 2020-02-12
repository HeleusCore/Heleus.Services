using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Messages;
using NetMQ;
using NetMQ.Sockets;

namespace Heleus.PushService
{
    public class PushServer : IDisposable, ILogger
    {
        static PushServer()
        {
            PushServiceMessage.RegisterPushServiceMessages();
        }

        static readonly Dictionary<string, PushServer> _servers = new Dictionary<string, PushServer>();

        public static PushServer GetPushServer(string bindAddress)
        {
            lock (_servers)
            {
                if (_servers.TryGetValue(bindAddress, out var reference))
                {
                    reference._refCount++;
                    Log.Trace($"RefCount increased to {reference._refCount} for {reference.BindAddress}.", reference);
                    return reference;
                }

                var server = new PushServer(bindAddress);
                _servers[bindAddress] = server;
                return server;
            }
        }

        public readonly string BindAddress;
        public string LogName => GetType().Name;

        NetMQPoller _poller = new NetMQPoller();
        PullSocket _socket = new PullSocket();
        int _refCount;
        readonly Dictionary<int, IPushMessageReceiver> _receivers = new Dictionary<int, IPushMessageReceiver>();


        PushServer(string bindAddress)
        {
            BindAddress = bindAddress;
            _refCount = 1;

            Log.Info($"New socket for address {bindAddress} created.", this);

            _socket.Bind(bindAddress);

            _socket.ReceiveReady += (s, a) =>
            {
                var messages = new List<byte[]>();
                for (var i = 0; i < 1000; i++)
                {
                    if (!a.Socket.TryReceiveFrameBytes(out var messageData))
                        break;

                    messages.Add(messageData);
                }

                Task.Run(() =>
                {
                    foreach (var messageData in messages)
                    {
                        try
                        {
                            using (var unpacker = new Unpacker(messageData))
                            {
                                var message = Message.Restore<PushServiceMessage>(unpacker);
                                if (message != null)
                                {
                                    if (_receivers.TryGetValue(message.ChainId, out var receiver))
                                        receiver.HandlePushServiceMessage(message);
                                    else
                                        Log.Warn($"PushMessage received for invalid chain {message.ChainId}.", this);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.HandleException(ex, this);
                        }
                    }
                });
            };

            _poller.Add(_socket);
            _poller.RunAsync();
        }

        public void AddPushMessageReceiver(IPushMessageReceiver pushMessageReceiver)
        {
            if (pushMessageReceiver != null)
            {
                _receivers[pushMessageReceiver.PushServiceChainId] = pushMessageReceiver;
            }
        }

        public void RemovePushMessageReceiver(IPushMessageReceiver pushMessageReceiver)
        {
            if (pushMessageReceiver != null)
            {
                _receivers.Remove(pushMessageReceiver.PushServiceChainId);
            }
        }

        ~PushServer()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            lock (_servers)
            {
                _refCount--;

                Log.Trace($"RefCount decreased to {_refCount} for {BindAddress}.", this);

                if (_refCount <= 0)
                {
                    Log.Trace($"RefCount decreased to 0 for {BindAddress}, disposing.", this);

                    _servers.Remove(BindAddress);
                    _receivers.Clear();

                    _poller?.Dispose();
                    _socket?.Dispose();

                    _poller = null;
                    _socket = null;
                }
            }
        }
    }
}
