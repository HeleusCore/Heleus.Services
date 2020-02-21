using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Heleus.Transactions;
using Heleus.Service;
using Heleus.Base;
using Heleus.Network.Client.Record;
using Heleus.Transactions.Features;
using Heleus.Chain;
using Heleus.Chain.Blocks;
using Heleus.ServiceHelper;

namespace Heleus.NoteService
{
    public class NoteService : IService, IServiceBlockHandler
    {
        IServiceHost _host;
        ErrorReportsService _errorResport;

        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, 0, AccountIndex.FeatureId);
        }

        public async Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            _host = host;

            var configuration = Service.ServiceHelper.GetConfiguration(configurationString);
            var dataPath = configuration[Service.ServiceHelper.ServiceDataPathKey];

            _errorResport = new ErrorReportsService();
            await _errorResport.Init(dataPath);


            return new ServiceResult(ServiceResultTypes.Ok, NoteServiceInfo.Version, NoteServiceInfo.Name);
        }

        public Task Stop()
        {
            _errorResport?.Dispose();
            _errorResport = null;

            return Task.CompletedTask;
        }

        public static bool IsValidNoteFile(string filePath)
        {
            try
            {
                var data = File.ReadAllBytes(filePath);
                using(var unpacker = new Unpacker(data))
                {
                    _ = new EncrytpedRecord<NoteRecord>(unpacker);
                    return true;
                }
            }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
            {

            }
            return false;
        }

        static ServiceResult AreAttachementItemsValid(IReadOnlyList<AttachementItem> items)
        {
            if (items == null || items.Count != 1)
                return new ServiceResult(ServiceResultTypes.False, (long)NoteUserCodes.InvalidAttachement);

            var item = items[0];

            if (item.Name == NoteServiceInfo.NoteFileName)
            {
                if (item.DataSize <= NoteServiceInfo.NoteMaxFileSize)
                    return ServiceResult.Ok;
            }

            return new ServiceResult(ServiceResultTypes.False, (long)NoteUserCodes.InvalidAttachement);
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            var result = tempFiles.Count == 1 ? ServiceResultTypes.Ok : ServiceResultTypes.False;
            var userCode = tempFiles.Count == 1 ? NoteUserCodes.None : NoteUserCodes.InvalidAttachement;

            if (result == ServiceResultTypes.Ok)
            {
                var file = tempFiles[0];
                if (!IsValidNoteFile(file.TempPath))
                {
                    result = ServiceResultTypes.False;
                    userCode = NoteUserCodes.InvalidAttachement;
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
        }


        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            if (serviceTransaction.TransactionType == ServiceTransactionTypes.Join)
                return Task.FromResult(ServiceResult.Ok);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)NoteUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            var result = ServiceResultTypes.False;
            var userCode = NoteUserCodes.InvalidTransaction;

            if (dataTransaction.PrivacyType == DataTransactionPrivacyType.PrivateData)
            {
                if (dataTransaction.HasOnlyFeature(AccountIndex.FeatureId) &&
                    dataTransaction.TryGetFeature<AccountIndex>(AccountIndex.FeatureId, out var accountIndex) && accountIndex.Index == NoteServiceInfo.NoteIndex)
                {
                    return Task.FromResult(AreAttachementItemsValid((dataTransaction as AttachementDataTransaction)?.Items));
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
        }

        public Task<ServiceResult> IsValidAttachementsRequest(Attachements attachements)
        {
            return Task.FromResult(AreAttachementItemsValid(attachements.Items));
        }

        public Task<byte[]> ClientMessage(long accountId, byte[] messageData)
        {
            return Task.FromResult<byte[]>(null);
        }

        public Task ClientErrorReports(long accountId, byte[] errorReports)
        {
            _errorResport?.QueueErrorReports(accountId, errorReports);
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
            var block = blockData.Block;

            foreach (var transaction in block.Transactions)
            {
                _host.MaintainChain.ProposeAccountRevenue(transaction.AccountId, transaction.Timestamp);
            }

            return Task.CompletedTask;
        }
    }
}
