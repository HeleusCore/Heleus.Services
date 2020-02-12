using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Chain.Blocks;
using Heleus.Messages;
using Heleus.PushService;
using Heleus.Service;
using Heleus.Service.Push;
using Heleus.ServiceHelper;
using Heleus.Transactions;
using Heleus.Transactions.Features;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace Heleus.StatusService
{

    public class StatusService : IService, IServicePushHandler, IServiceBlockHandler, IServiceErrorReportsHandler, IServiceUriDataHandler, ILogger
    {
        public string LogName => GetType().Name;

        static readonly JSchema _statusSchema;
        IServiceHost _host;

        bool _running;

        ErrorReportsService _errorResport;
        StatusSqliteService _statusSql;
        PushServiceClient _pushClient;

        readonly object _lock = new object();
        List<AttachementDataTransaction> _trending = new List<AttachementDataTransaction>();
        List<AttachementDataTransaction> _notifications = new List<AttachementDataTransaction>();

        TrendingResult _latestTrendingResult;

        static StatusService()
        {
            var statusSchema = @"{
                  'type': 'object',
                  'required': ['m'],
                  'additionalProperties': false,
                  'properties': {
                    'm': {
                      'type': 'string',
                      'minLength': 2,
                      'maxLength': 1024
                    },
                    'l': {
                      'type': 'string',
                      'pattern': '^(http:|https:)',
                      'maxLength': 1024
                    }
                  }
                }";

            _statusSchema = JSchema.Parse(statusSchema);
        }

        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, StatusServiceInfo.FanChainIndex, Receiver.FeatureId);
            options.EnableChainFeature(ChainType.Data, StatusServiceInfo.FanChainIndex, Fan.FeatureId);

            options.EnableChainFeature(ChainType.Data, StatusServiceInfo.StatusDataChainIndex, Payload.FeatureId);
            options.EnableChainFeature(ChainType.Data, StatusServiceInfo.StatusDataChainIndex, AccountIndex.FeatureId);
        }

        public async Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            _host = host;
            _running = true;

            var configuration = Service.ServiceHelper.GetConfiguration(configurationString);
            var dataPath = configuration[Service.ServiceHelper.ServiceDataPathKey];

            _statusSql = new StatusSqliteService();
            await _statusSql.Init(dataPath);

            _errorResport = new ErrorReportsService();
            await _errorResport.Init(dataPath);

            _pushClient = new PushServiceClient(configuration);

            TaskRunner.Run(TrendingLoop);
            TaskRunner.Run(NotificationLoop);

            return new ServiceResult(ServiceResultTypes.Ok, StatusServiceInfo.Version, StatusServiceInfo.Name);
        }

        public Task Stop()
        {
            _running = false;

            _statusSql?.Dispose();
            _statusSql = null;

            _errorResport?.Dispose();
            _errorResport = null;

            _pushClient?.Dispose();
            _pushClient = null;

            return Task.CompletedTask;
        }

        public static ServiceUserCodes IsValidJsonFile(string filePath)
        {
            try
            {
                var jsonText = File.ReadAllText(filePath);
                var json = JObject.Parse(jsonText);

                var valid = json.IsValid(_statusSchema, out IList<ValidationError> errors);

                if (!valid)
                {
                    foreach (var error in errors)
                    {
                        var path = error.Path;
                        var type = error.ErrorType;

                        if (path == "m")
                        {
                            if (type == ErrorType.MaximumLength)
                            {
                                return ServiceUserCodes.InvalidStatusMessageLength;
                            }
                        }
                        else if (path == "l")
                        {
                            if (type == ErrorType.MaximumLength)
                            {
                                return ServiceUserCodes.InvalidStatusLink;
                            }

                            if (type == ErrorType.Pattern)
                            {
                                return ServiceUserCodes.InvalidStatusLink;
                            }
                        }
                    }
                }

                return valid ? ServiceUserCodes.None : ServiceUserCodes.InvalidStatusJson;
            }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
            {
            }

            return ServiceUserCodes.InvalidStatusJson;
        }

        static ServiceResult AreAttachementItemsValid(IReadOnlyList<AttachementItem> items)
        {
            if (items.Count <= 0 || items.Count > 2)
                return new ServiceResult(ServiceResultTypes.False, (long)ServiceUserCodes.InvalidAttachementItems);

            var userCode = ServiceUserCodes.InvalidAttachementItems;
            var hasStatusJson = false;
            var hasImage = false;

            foreach (var item in items)
            {
                if (item.Name == StatusServiceInfo.StatusJsonFileName)
                {
                    if (hasStatusJson)
                    {
                        hasStatusJson = false;
                        break;
                    }
                    hasStatusJson = true;
                    continue;
                }

                if (item.Name == StatusServiceInfo.ImageFileName)
                {
                    if (hasImage)
                    {
                        hasStatusJson = false;
                        break;
                    }
                    hasImage = true;
                    continue;
                }

                if (!hasStatusJson)
                    return new ServiceResult(ServiceResultTypes.False, (long)userCode);
            }

            if (hasStatusJson)
                return ServiceResult.Ok;

            return new ServiceResult(ServiceResultTypes.False, (long)userCode);
        }

        public Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            var result = tempFiles.Count >= 1 && tempFiles.Count <= 2 ? ServiceResultTypes.Ok : ServiceResultTypes.False;
            var userCode = result == ServiceResultTypes.Ok ? ServiceUserCodes.None : ServiceUserCodes.InvalidAttachementItems;

            if (result == ServiceResultTypes.Ok)
            {
                foreach (var file in tempFiles)
                {
                    if (file.Item.Name == StatusServiceInfo.StatusJsonFileName)
                    {
                        var jsonCode = IsValidJsonFile(file.TempPath);
                        if (jsonCode != ServiceUserCodes.None)
                        {
                            result = ServiceResultTypes.False;
                            userCode = jsonCode;

                            break;
                        }
                    }
                    else if (file.Item.Name == StatusServiceInfo.ImageFileName)
                    {
                        var img = Image.IsValidImage(file.TempPath, StatusServiceInfo.ImageMaxFileSize);
                        if (!img.IsValid)
                        {
                            result = ServiceResultTypes.False;

                            userCode = ServiceUserCodes.InvalidImageFormat;
                            if (img.IsInvalidFileSize)
                                userCode = ServiceUserCodes.InvalidImageFileSize;

                            break;
                        }

                        if (img.Width != img.Height || img.Width > StatusServiceInfo.ImageDimension || img.Height > StatusServiceInfo.ImageDimension)
                        {
                            result = ServiceResultTypes.False;
                            userCode = ServiceUserCodes.InvalidImageDimensions;

                            break;
                        }
                    }
                    else
                    {
                        result = ServiceResultTypes.False;
                        userCode = ServiceUserCodes.InvalidAttachementItems;

                        break;
                    }
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
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
            var index = dataTransaction.GetFeature<AccountIndex>(AccountIndex.FeatureId)?.Index;

            if (dataTransaction.PrivacyType == DataTransactionPrivacyType.PublicData)
            {
                var chainIndex = dataTransaction.ChainIndex;
                if(chainIndex == StatusServiceInfo.FanChainIndex)
                {
                    if (type == DataTransactionTypes.FeatureRequest && dataTransaction.HasFeatureRequest(FanRequest.FanRequestId))
                    {
                        result = ServiceResultTypes.Ok;
                        userCode = ServiceUserCodes.None;
                    }
                }
                else if(chainIndex == StatusServiceInfo.StatusDataChainIndex)
                {
                    if (type == DataTransactionTypes.Attachement)
                    {
                        if (index == StatusServiceInfo.MessageIndex)
                            return Task.FromResult(AreAttachementItemsValid((dataTransaction as AttachementDataTransaction).Items));
                    }
                }
            }

            return Task.FromResult(new ServiceResult(result, (long)userCode));
        }

        public Task<ServiceResult> IsValidAttachementsRequest(Attachements attachements)
        {
            return Task.FromResult(AreAttachementItemsValid(attachements.Items));
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

            foreach(var transaction in block.Transactions)
            {
                var index = transaction.GetFeature<AccountIndex>(AccountIndex.FeatureId)?.Index;
                if (transaction.TransactionType == DataTransactionTypes.Attachement && index == StatusServiceInfo.MessageIndex)
                {
                    lock (_lock)
                    {
                        var a = transaction as AttachementDataTransaction;
                        _trending.Add(a);
                        _notifications.Add(a);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task<IPackable> QueryStaticUriData(string path)
        {
            return Task.FromResult<IPackable>(null);
        }

        public async Task<IPackable> QueryDynamicUriData(string path)
        {
            var segments = path.Split('/');

            if (segments.Length == 2)
            {
                if (segments[0] == "trending")
                {
                    lock (_lock)
                        return _latestTrendingResult;
                }
            }

            if(_pushClient != null)
                return await _pushClient.QueryDynamicUriData(path);

            return null;
        }

        async Task QueryTrending()
        {
            var @new = await _statusSql.SelectNewAccounts(StatusServiceInfo.MaxTrendingItems);
            var popular = await _statusSql.SelectPopularAccounts(StatusServiceInfo.MaxTrendingItems);
            var recent = await _statusSql.SelectRecentAccounts(StatusServiceInfo.MaxTrendingItems);

            var trending = new TrendingResult(@new, popular, recent);
            lock (_lock)
                _latestTrendingResult = trending;
        }

        static string ShortenString(string text, int cut = 256, int maxLength = 288)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (cut > maxLength)
            {
                var tmp = cut;
                cut = maxLength;
                maxLength = tmp;
            }

            if (text.Length <= maxLength)
                return text;

            for(var i = cut; i < text.Length; i++)
            {
                if(char.IsWhiteSpace(text[i]))
                {
                    return text.Substring(0, i);// + "\u2026";
                }
            }

            return text.Substring(0, maxLength);
        }

        async Task NotificationLoop()
        {
            while (_running)
            {
                await Task.Delay(1000);

                List<AttachementDataTransaction> notifications = null;
                lock (_lock)
                {
                    if (_notifications.Count > 0)
                    {
                        notifications = _notifications;
                        _notifications = new List<AttachementDataTransaction>();
                    }
                }

                if (notifications != null)
                {
                    foreach (var transaction in notifications)
                    {
                        try
                        {
                            var transactionId = transaction.TransactionId;
                            var accountId = transaction.AccountId;

                            var filePath = _host.GetDataChain(0).GetLocalAttachementPath(transactionId, transaction.AttachementKey, StatusServiceInfo.StatusJsonFileName);
                            var jsonText = File.ReadAllText(filePath);
                            var m = JsonConvert.DeserializeObject<StatusJson>(jsonText);
                            if (m != null)
                            {
                                string title = null;
                                var payload = transaction.GetFeature<Payload>(Payload.FeatureId)?.PayloadData;
                                if (payload != null)
                                    title = Encoding.UTF8.GetString(payload);

                                if (string.IsNullOrEmpty(title))
                                    title = accountId.ToString();

                                var message = ShortenString(m.m);

                                if (_pushClient != null)
                                    _pushClient.SendNotification(new PushNotification(Chain.Index.New().Add(accountId).Build(), 1) { NotificationTitle = title, NotificationMessage = message, NotificationScheme = $"6/-/{accountId}/{transactionId}" });
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        async Task TrendingLoop()
        {
            await QueryTrending();

            while(_running)
            {
                await Task.Delay(10000);

                List<AttachementDataTransaction> trending = null;
                lock (_lock)
                {
                    if (_trending.Count > 0)
                    {
                        trending = _trending;
                        _trending = new List<AttachementDataTransaction>();
                    }
                }

                if(trending != null)
                {
                    var cleanupRecent = false;
                    var cleanupPopular = false;
                    var vacuum = false;

                    foreach (var transaction in trending)
                    {
                        var accountId = transaction.AccountId;
                        var accountIndex = transaction.GetFeature<AccountIndex>(AccountIndex.FeatureId);
                        var messageCount = accountIndex.TransactionCount;

                        var featureAccount = _host.GetDataChain(StatusServiceInfo.FanChainIndex).GetFeatureAccount(accountId);
                        var fan = featureAccount.GetFeatureContainer<FanContainer>(Fan.FeatureId);
                        var fans = fan?.GetFans();

                        // new
                        if (messageCount > 5 && messageCount <= 50)
                        {
                            await _statusSql.InsertNewAccount(accountId);
                        }
                        else if (messageCount > 50 && messageCount < 60) // just to make sure it will be removed, add some paddint
                        {
                            if (await _statusSql.DeleteNewAccount(accountId))
                                vacuum = true;
                        }

                        if (fans != null)
                        {
                            var fanCount = fans.Fans.Count;

                            // recent
                            if (fanCount > 5 && messageCount > 5)
                            {
                                if (await _statusSql.DeleteRecentAccount(accountId))
                                {
                                    cleanupRecent = true;
                                    vacuum = true;
                                }
                                await _statusSql.InsertRecentAccount(accountId);
                            }

                            // popular
                            if (fanCount > 100 && messageCount > 100)
                            {
                                if (await _statusSql.DeletePopularAccount(accountId))
                                {
                                    cleanupPopular = true;
                                    vacuum = true;
                                }
                                await _statusSql.InsertPopularAccount(accountId);
                            }
                        }
                    }

                    if (cleanupPopular)
                        await _statusSql.CleanupPopularAccounts(StatusServiceInfo.MaxTrendingItems);
                    if (cleanupRecent)
                        await _statusSql.CleanupRecentAccounts(StatusServiceInfo.MaxTrendingItems);
                    if (vacuum)
                        await _statusSql.Vacuum();

                    await QueryTrending();
                }
            }
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
    }
}
