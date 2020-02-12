using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Heleus.Transactions;
using Heleus.Service;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Heleus.Chain;
using Heleus.Transactions.Features;

namespace Heleus.VerifyService
{
    public class VerifyService : IService
    {
        static readonly JSchema _verifySchema;

        static VerifyService()
        {
            var verifySchema = @"{
                  'type': 'object',
                  'required': ['description', 'files'],
                  'additionalProperties': false,
                  'properties': {
                    'description': {
                      'type': 'string'
                    },
                    'link': {
                      'type': ['string', 'null']
                    },
                    'files': {
                      'type': 'array',
                      'additionalItems': false,
                      'minItems': 1,
                      'items': {
                        'type': 'object',
                        'required': ['name', 'hash', 'hashtype', 'length'],
                        'additionalProperties': false,
                        'properties': {
                          'name': {
                            'type': 'string'
                          },
                          'hash': {
                            'type': 'string'
                          },
                          'hashtype': {
                            'type': 'string'
                          },
                          'link': {
                            'type': ['string', 'null']
                          },
                          'length': {
                            'type': ['integer']
                          }
                        }
                      }
                    }
                  }
                }";

            _verifySchema = JSchema.Parse(verifySchema);
        }

        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, 0, AccountIndex.FeatureId);
        }

        public Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            //var configuration = HeleusService.GetConfiguration(configurationString);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.Ok, VerifyServiceInfo.Version, VerifyServiceInfo.Name));
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }

        static ServiceResult AreAttachementItemsValid(IReadOnlyList<AttachementItem> items)
        {
            if (items == null || items.Count != 1)
                return new ServiceResult(ServiceResultTypes.False, (long)VerifyUserCodes.InvalidAttachement);

            var item = items[0];

            var userCode = VerifyUserCodes.None;
            if (item.Name == VerifyServiceInfo.JsonFileName)
            {
                if (item.DataSize <= VerifyServiceInfo.JsonMaxFileSize)
                    return ServiceResult.Ok;

                userCode = VerifyUserCodes.InvalidJsonFizeSize;
            }

            return new ServiceResult(ServiceResultTypes.False, (long)userCode);
        }

        public static bool IsValidJsonFile(string filePath)
        {
            try
            {
                var jsonText = File.ReadAllText(filePath);
                if (jsonText.Length > VerifyServiceInfo.JsonMaxFileSize)
                    return false;

                return JObject.Parse(jsonText).IsValid(_verifySchema);
            }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
            {

            }
            return false;
        }

        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            if (serviceTransaction.TransactionType == ServiceTransactionTypes.Join)
                return Task.FromResult(ServiceResult.Ok);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)VerifyUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            var result = ServiceResultTypes.False;
            var userCode = VerifyUserCodes.InvalidTransaction;

            if (dataTransaction.PrivacyType == DataTransactionPrivacyType.PublicData)
            {
                if (dataTransaction.HasOnlyFeature(AccountIndex.FeatureId) &&
                    dataTransaction.TryGetFeature<AccountIndex>(AccountIndex.FeatureId, out var accountIndex) && accountIndex.Index == VerifyServiceInfo.VerifyIndex)
                {
                    return Task.FromResult(AreAttachementItemsValid((dataTransaction as AttachementDataTransaction)?.Items));
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            var result = tempFiles.Count != 1 ? ServiceResultTypes.False : ServiceResultTypes.Ok;
            var userCode = tempFiles.Count != 1 ? (long)VerifyUserCodes.InvalidAttachement : 0;

            if (result == ServiceResultTypes.Ok)
            {
                var file = tempFiles[0];
                if (!IsValidJsonFile(file.TempPath))
                {
                    result = ServiceResultTypes.False;
                    userCode = (long)VerifyUserCodes.InvalidJson;
                }
            }

            return Task.FromResult(new ServiceResult(result, userCode));
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
            //var reports = HeleusService.GerErrorReports(errorReports);
            return Task.CompletedTask;
        }
    }
}
