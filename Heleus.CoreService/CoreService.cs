using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Chain.Blocks;
using Heleus.Messages;
using Heleus.PushService;
using Heleus.Service;
using Heleus.Service.Push;
using Heleus.ServiceHelper;
using Heleus.Transactions;

namespace Heleus.CoreService
{
    public static class CoreServiceInfo
    {
        public const long Version = 1;
        public const string Name = "Core Service";

        public const int ChainId = Protocol.CoreChainId;
    }

    public class CoreService : IService, IServiceBlockHandler, IServiceErrorReportsHandler, IServicePushHandler, IServiceUriDataHandler
    {
        ErrorReportsService _errorResport;
        PushServiceClient _pushService;

        readonly Chain.Index _notificationIndex = Chain.Index.New().Add((short)1).Build();

        public void Initalize(ServiceOptions options)
        {
        }

        public async Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            var configuration = Service.ServiceHelper.GetConfiguration(configurationString);
            var dataPath = configuration[Service.ServiceHelper.ServiceDataPathKey];

            _errorResport = new ErrorReportsService();
            await _errorResport.Init(dataPath);

            //_pushService = new PushServiceClient(configuration);

            return new ServiceResult(ServiceResultTypes.Ok, CoreServiceInfo.Version, CoreServiceInfo.Name);
        }

        public Task Stop()
        {
            _errorResport?.Dispose();
            _errorResport = null;

            _pushService?.Dispose();
            _pushService = null;

            return Task.CompletedTask;
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, 0));
        }

        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, 0));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, 0));
        }

        public Task<ServiceResult> IsValidAttachementsRequest(Attachements attachements)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, 0));
        }

        public Task ClientErrorReports(long accountId, byte[] errorReports)
        {
            _errorResport?.QueueErrorReports(accountId, errorReports);
            return Task.CompletedTask;
        }

        public Task PushTokenInfo(ClientPushTokenMessageAction action, PushTokenInfo pushTokenInfo, IServiceRemoteRequest request)
        {
            _pushService?.SendPushTokenInfo(action, pushTokenInfo, request);
            return Task.CompletedTask;
        }

        public Task PushSubscription(PushSubscription pushSubscription, IServiceRemoteRequest request)
        {
            _pushService?.SendPushSubscription(pushSubscription, request);
            return Task.CompletedTask;
        }

        public Task NewBlockData(BlockData<CoreBlock> blockData)
        {
            var block = blockData.Block;
            var senders = new HashSet<long>();
            var receivers = new HashSet<long>();

            foreach (var transaction in block.Transactions)
            {
                if (transaction.TransactionType == CoreTransactionTypes.Transfer)
                {
                    var transferTransaction = transaction as TransferCoreTransaction;

                    senders.Add(transaction.AccountId);
                    receivers.Add(transferTransaction.ReceiverAccountId);
                }
            }

            if (receivers.Count > 0)
                _pushService?.SendNotification(new PushNotification(receivers.ToList(), _notificationIndex, 1));
            if (senders.Count > 0)
                _pushService?.SendNotification(new PushNotification(senders.ToList(), _notificationIndex, 2));

            return Task.CompletedTask;
        }

        public Task NewBlockData(BlockData<ServiceBlock> blockData)
        {
            throw new NotImplementedException();
        }

        public Task NewBlockData(BlockData<DataBlock> blockData)
        {
            throw new NotImplementedException();
        }

        public Task<IPackable> QueryStaticUriData(string path)
        {
            return Task.FromResult<IPackable>(null);
        }

        public async Task<IPackable> QueryDynamicUriData(string path)
        {
            if (_pushService != null)
            {
                var result = await _pushService.QueryDynamicUriData(path);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
