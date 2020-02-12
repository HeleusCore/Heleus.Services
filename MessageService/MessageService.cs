using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Chain.Blocks;
using Heleus.Messages;
using Heleus.Network.Client.Record;
using Heleus.PushService;
using Heleus.Service;
using Heleus.Service.Push;
using Heleus.ServiceHelper;
using Heleus.Transactions;
using Heleus.Transactions.Features;

namespace Heleus.MessageService
{
    public class MessageService : IService, IServicePushHandler, IServiceBlockHandler, IServiceErrorReportsHandler
    {
        ErrorReportsService _errorResport;
        PushServiceClient _pushClient;

        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.FriendChainIndex, Receiver.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.FriendChainIndex, Friend.FeatureId);

            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, PreviousAccountTransaction.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, Receiver.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, AccountIndex.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, SharedAccountIndex.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, Data.FeatureId);
            options.EnableChainFeature(ChainType.Data, MessageServiceInfo.MessageDataChainIndex, EnforceReceiverFriend.FeatureId);
        }

        public async Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            var configuration = Service.ServiceHelper.GetConfiguration(configurationString);
            var dataPath = configuration[Service.ServiceHelper.ServiceDataPathKey];

            _errorResport = new ErrorReportsService();
            await _errorResport.Init(dataPath);

            _pushClient = new PushServiceClient(configuration);

            return new ServiceResult(ServiceResultTypes.Ok, MessageServiceInfo.Version, MessageServiceInfo.Name);
        }

        public Task Stop()
        {
            _errorResport?.Dispose();
            _errorResport = null;

            _pushClient?.Dispose();
            _pushClient = null;

            return Task.CompletedTask;
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, 0));
        }

        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            if (serviceTransaction.TransactionType == ServiceTransactionTypes.Join)
                return Task.FromResult(ServiceResult.Ok);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ServiceUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            var type = dataTransaction.TransactionType;

            var result = ServiceResultTypes.False;
            var userCode = ServiceUserCodes.InvalidTransaction;
            var privacyType = dataTransaction.PrivacyType;
            var chainIndex = dataTransaction.ChainIndex;

            if (privacyType == DataTransactionPrivacyType.PublicData)
            {
                if (chainIndex == MessageServiceInfo.MessageDataChainIndex)
                {
                    var index = dataTransaction.GetFeature<AccountIndex>(AccountIndex.FeatureId)?.Index;
                    var indexType = MessageServiceInfo.GetIndexType(index);

                    if (indexType == MessageRecordTypes.InboxName)
                    {
                        if (dataTransaction.TryGetFeature<Data>(Data.FeatureId, out var data) && data.Items.Count == 1 && data.GetItem(MessageServiceInfo.MessageDataIndex, out var item))
                        {
                            if (item.Length > 0 && item.Length < (MessageServiceInfo.MaxInboxNameLength * 4))
                            {
                                using (var unpacker = new Unpacker(item.Data))
                                {
                                    var record = new InboxNameRecord(unpacker);
                                    if (record.Title.Length <= MessageServiceInfo.MaxInboxNameLength)
                                    {
                                        result = ServiceResultTypes.Ok;
                                        userCode = ServiceUserCodes.None;
                                    }
                                    else
                                    {
                                        userCode = ServiceUserCodes.InboxNameInvalid;
                                    }
                                }
                            }
                            else
                            {
                                userCode = ServiceUserCodes.InboxNameInvalid;
                            }
                        }
                    }
                }
            }
            else if (privacyType == DataTransactionPrivacyType.PrivateData)
            {
                if (chainIndex == MessageServiceInfo.FriendChainIndex)
                {
                    if (dataTransaction.HasFeatureRequest(FriendRequest.FriendRequestId))
                    {
                        result = ServiceResultTypes.Ok;
                        userCode = ServiceUserCodes.None;
                    }
                }
                else if(chainIndex == MessageServiceInfo.MessageDataChainIndex)
                {
                    var index = dataTransaction.GetFeature<SharedAccountIndex>(SharedAccountIndex.FeatureId)?.Index;
                    var indexType = MessageServiceInfo.GetIndexType(index);

                    if (indexType == MessageRecordTypes.Message && dataTransaction.HasFeature(EnforceReceiverFriend.FeatureId) && dataTransaction.HasFeature(PreviousAccountTransaction.FeatureId))
                    {
                        if (dataTransaction.TryGetFeature<Data>(Data.FeatureId, out var data) && data.Items.Count == 1 && data.GetItem(MessageServiceInfo.MessageDataIndex, out var item))
                        {
                            var (a1, k1, a2, k2) = MessageServiceInfo.GetAccountsAndKeyIndices(index);
                            var receiverData = dataTransaction.GetFeature<Receiver>(Receiver.FeatureId);

                            if (receiverData != null && receiverData.Receivers.Count == 1)
                            {
                                var accountId = dataTransaction.AccountId;
                                var keyIndex = dataTransaction.SignKeyIndex;
                                var receiverId = receiverData.Receivers[0];

                                if ((accountId == a1 || accountId == a2) && (keyIndex == k1 || keyIndex == k2))
                                {
                                    if (receiverId == a1 || receiverId == a2)
                                    {
                                        try
                                        {
                                            using (var unpacker = new Unpacker(item.Data))
                                            {
                                                _ = new EncrytpedRecord<MessageRecord>(unpacker);
                                                result = ServiceResultTypes.Ok;
                                                userCode = ServiceUserCodes.None;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
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
            _pushClient?.SendPushTokenInfo(action, pushTokenInfo, request);
            return Task.CompletedTask;
        }

        public Task PushSubscription(PushSubscription pushSubscription, IServiceRemoteRequest request)
        {
            _pushClient?.SendPushSubscription(pushSubscription, request);
            return Task.CompletedTask;
        }

        public Task NewBlockData(BlockData<CoreBlock> blockData)
        {
            return Task.CompletedTask;
        }

        public Task NewBlockData(BlockData<ServiceBlock> blockData)
        {
            return Task.CompletedTask;
        }

        public Task NewBlockData(BlockData<DataBlock> blockData)
        {
            return Task.CompletedTask;
        }
    }
}
