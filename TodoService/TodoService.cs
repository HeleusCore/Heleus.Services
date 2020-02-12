using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Transactions;
using Heleus.Service;
using Heleus.Base;
using Heleus.Network.Client.Record;
using Heleus.Transactions.Features;
using Heleus.Chain;

namespace Heleus.TodoService
{
    public class TodoService : IService
    {
        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.GroupChainIndex, Receiver.FeatureId);
            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.GroupChainIndex, GroupAdministration.FeatureId);

            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.TodoDataChainIndex, Receiver.FeatureId);
            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.TodoDataChainIndex, TransactionTarget.FeatureId);
            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.TodoDataChainIndex, Group.FeatureId);
            options.EnableChainFeature(ChainType.Data, TodoServiceInfo.TodoDataChainIndex, Data.FeatureId);
        }

        public Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            //var configuration = HeleusService.GetConfiguration(configurationString);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.Ok, TodoServiceInfo.Version, TodoServiceInfo.Name));
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }

        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            if (serviceTransaction.TransactionType == ServiceTransactionTypes.Join)
                return Task.FromResult(ServiceResult.Ok);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ServiceUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            var result = ServiceResultTypes.False;
            var userCode = ServiceUserCodes.InvalidTransaction;

            if (dataTransaction.PrivacyType == DataTransactionPrivacyType.PrivateData)
            {
                if (dataTransaction.ChainIndex == TodoServiceInfo.GroupChainIndex)
                {
                    if (dataTransaction.HasOnlyFeature(GroupAdministration.FeatureId) && dataTransaction.GetFeatureRequest<GroupRegistrationRequest>(out var request))
                    {
                        if ((request.GroupFlags & GroupFlags.AdminOnlyInvitation) != 0)
                        {
                            result = ServiceResultTypes.Ok;
                        }
                    }
                    else if (dataTransaction.HasFeatureRequest(GroupAdministrationRequest.GroupAdministrationRequestId))
                    {
                        result = ServiceResultTypes.Ok;
                    }
                }
                else if (dataTransaction.ChainIndex == TodoServiceInfo.TodoDataChainIndex)
                {
                    if (dataTransaction.TryGetFeature<Data>(Data.FeatureId, out var data) && data.Count == 1 && data.GetItem(TodoServiceInfo.TodoDataItemIndex, out var item))
                    {
                        var groupIndex = dataTransaction.GetFeature<Group>(Group.FeatureId)?.GroupIndex;
                        if (groupIndex != null)
                        {
                            if (groupIndex == TodoServiceInfo.TodoListNameIndex)
                            {
                                using (var unpacker = new Unpacker(item.Data))
                                {
                                    _ = new EncrytpedRecord<TodoListNameRecord>(unpacker);
                                    result = ServiceResultTypes.Ok;
                                }
                            }
                            else if (groupIndex == TodoServiceInfo.TodoTaskIndex)
                            {
                                using (var unpacker = new Unpacker(item.Data))
                                {
                                    _ = new EncrytpedRecord<TodoTaskRecord>(unpacker);
                                    result = ServiceResultTypes.Ok;
                                }

                            }
                            else if (groupIndex == TodoServiceInfo.TodoTaskStatusIndex)
                            {
                                using (var unpacker = new Unpacker(item.Data))
                                {
                                    _ = new TodoTaskStatusRecord(unpacker);
                                    result = ServiceResultTypes.Ok;
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
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ServiceUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ServiceUserCodes.InvalidTransaction));
        }

        public Task<byte[]> ClientMessage(long accountId, byte[] messageData)
        {
            return Task.FromResult<byte[]>(null);
        }

        public Task ClientErrorReports(long accountId, byte[] errorReports)
        {
            //var reports = HeleusService.GerErrorReports(errorReports);
            return Task.CompletedTask;
        }
    }
}
