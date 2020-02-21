using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Messages;
using Heleus.Service;
using Heleus.Service.Push;

namespace Heleus.PushService
{
    public class PushServiceClient : IPushMessageReceiver, IDisposable, ILogger
    {
        public int PushServiceChainId { get; private set; }
        public string LogName => GetType().Name;

        readonly Chain.Index _subscriptionIndex = Chain.Index.New().Add((short)0).Build();

        bool _running;
        bool _receivedPong;

        int _pushClientId;
        PushClient _pushClient;
        PushServer _pushServer;

        readonly LazyLookupTable<long, IServiceRemoteRequest> _remoteRequests = new LazyLookupTable<long, IServiceRemoteRequest> { LifeSpan = TimeSpan.FromSeconds(15), Depth = 2 };
        readonly LazyLookupTable<long, TaskCompletionSource<PushSubscriptionResponse>> _remoteQueries = new LazyLookupTable<long, TaskCompletionSource<PushSubscriptionResponse>> { LifeSpan = TimeSpan.FromSeconds(15), Depth = 2 };

        public PushServiceClient(IReadOnlyDictionary<string, string> configuration)
        {
            PushServiceChainId = Service.ServiceHelper.GetServiceChainId(configuration);

            configuration.TryGetValue(PushServiceInfo.DefaultPushServerBindAddressConfigProperty, out var serverBindAddress);
            serverBindAddress = serverBindAddress ?? PushServiceInfo.DefaultPushServerBindAddress;
            configuration.TryGetValue(PushServiceInfo.DefaultPushClientBindAddressConfigProperty, out var clientBindAddress);
            clientBindAddress = clientBindAddress ?? PushServiceInfo.DefaultPushClientBindAddress;

            _pushClientId = PushServiceInfo.DefaultPushClientId;
            if (configuration.TryGetValue(PushServiceInfo.DefaultPushClientIdConfigProperty, out var clientIdStr))
            {
                if (int.TryParse(clientIdStr, out var clientId))
                {
                    _pushClientId = clientId;
                }
            }

            Log.Info($"Starting PushServiceClient ({clientBindAddress}) with Id {_pushClient} for chain {PushServiceChainId} with server {serverBindAddress}.", this);

            _pushClient = PushClient.GetPushClient(serverBindAddress);

            _pushServer = PushServer.GetPushServer(clientBindAddress);
            _pushServer.AddPushMessageReceiver(this);

            _running = true;
            TaskRunner.Run(() => PingLoop());
        }

        public Task<PushSubscriptionResponse> QueryDynamicUriData(string path)
        {
            var segments = path.Split('/');

            if (segments.Length == 4)
            {
                if (segments[0] == "pushservice")
                {
                    if (long.TryParse(segments[2], out var accountId))
                    {
                        var valid = false;
                        var action = PushSubscriptionAction.Query;
                        if (segments[1] == "query")
                        {
                            action = PushSubscriptionAction.Query;
                            valid = true;
                        }
                        else if (segments[1] == "lastupdate")
                        {
                            action = PushSubscriptionAction.LastUpdate;
                            valid = true;
                        }

                        if(valid)
                        {
                            var requestCode = Rand.NextLong();

                            var source = new TaskCompletionSource<PushSubscriptionResponse>();

                            _remoteQueries[requestCode] = source;
                            _pushClient.SendRemoteMessage(new PushServiceSubscriptionMessage(PushServiceSubscriptionMessageSender.ServiceUri, new PushSubscription(action, accountId, _subscriptionIndex), requestCode, PushServiceChainId, _pushClientId));

                            return source.Task;
                        }
                    }
                }
            }

            return Task.FromResult<PushSubscriptionResponse>(null);
        }

        public void HandlePushServiceMessage(PushServiceMessage message)
        {
            var requestCode = message.RequestCode;
            var messageType = message.MessageType;

            if(messageType == PushServiceMessageTypes.PushTokenResponse)
            {
                if (_remoteRequests.TryGetValue(requestCode, out var request))
                {
                    var response = message as PushServiceTokenResponseMessage;

                    request.RemoteHost.SendPushTokenResponse(response.Result, request);
                    _remoteRequests.Remove(requestCode);
                }
            }
            else if (messageType == PushServiceMessageTypes.SubscriptionResponse)
            {
                var response = message as PushServiceSubscriptionResponseMessage;
                if (response.Sender == PushServiceSubscriptionMessageSender.ServiceClient)
                {
                    if (_remoteRequests.TryGetValue(requestCode, out var request))
                    {
                        request.RemoteHost.SendPushSubscriptionResponse(response.Response, request);
                        _remoteRequests.Remove(requestCode);
                    }
                }
                else if (response.Sender == PushServiceSubscriptionMessageSender.ServiceUri)
                {
                    if(_remoteQueries.TryGetValue(requestCode, out var source))
                    {
                        source.SetResult(response.Response);
                        _remoteQueries.Remove(requestCode);
                    }
                }
            }
            else if (messageType == PushServiceMessageTypes.Pong)
            {
                if (message.ClientId == _pushClientId)
                {
                    if (Log.LogTrace)
                        Log.Trace($"Pong from PushServer {_pushServer.BindAddress} received!", this);

                    _receivedPong = true;
                }
            }
        }

        public void SendPushTokenInfo(ClientPushTokenMessageAction action, PushTokenInfo pushTokenInfo, IServiceRemoteRequest request)
        {
            if (_pushClient != null)
            {
                if (action == ClientPushTokenMessageAction.Register)
                    _pushClient.SendRemoteMessage(new PushServiceTokenRegistrationMessage(pushTokenInfo, request.RequestCode, PushServiceChainId, _pushClientId));
                else
                    _pushClient.SendRemoteMessage(new PushServiceTokenRemoveMessage(pushTokenInfo, request.RequestCode, PushServiceChainId, _pushClientId));

                _remoteRequests[request.RequestCode] = request;
            }
        }

        public void SendPushSubscription(PushSubscription pushSubscription, IServiceRemoteRequest request)
        {
            if (_pushClient != null)
            {
                var requestCode = request.RequestCode;

                _remoteRequests[requestCode] = request;
                _pushClient.SendRemoteMessage(new PushServiceSubscriptionMessage(PushServiceSubscriptionMessageSender.ServiceClient, pushSubscription, requestCode, PushServiceChainId, _pushClientId));
            }
        }

        public void SendNotification(PushNotification notification)
        {
            if (_pushClient != null)
            {
                _pushClient.SendRemoteMessage(new PushServiceNotificationMessage(notification, PushServiceChainId, _pushClientId));
            }
        }

        async Task PingLoop()
        {
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            while (_running)
            {
                var pushServer = _pushServer;
                var pushClient = _pushClient;

                if (pushServer == null || pushClient == null)
                    break;

                if (Log.LogTrace)
                    Log.Trace($"Sending ping to PushServer at {_pushServer?.BindAddress} with client id {_pushClientId}.", this);

                _receivedPong = false;
                pushClient.SendRemoteMessage(new PushServicePingMessage(PushServiceChainId, _pushClientId));

                await Task.Delay(TimeSpan.FromSeconds(5));

                if (!_receivedPong)
                    Log.Warn($"No response from PushServer {_pushServer?.BindAddress} received!", this);

                //await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        ~PushServiceClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            _running = false;
            _pushClient?.Dispose();
            _pushClient = null;

            _pushServer?.Dispose();
            _pushServer = null;

            GC.SuppressFinalize(this);
        }
    }
}
