using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Heleus.Chain.Blocks;
using Heleus.Transactions;
using Heleus.Service;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Heleus.ServiceHelper;
using Heleus.Base;
using Heleus.Transactions.Features;
using Heleus.Chain;

namespace Heleus.ProfileService
{
    public class ProfileService : IService, IServiceBlockHandler, IServiceUriDataHandler, IServiceErrorReportsHandler, ILogger
    {
        public string LogName => GetType().Name;

        JSchema _profileSchema;
        readonly LazyLookupTable<string, long> _reserved = new LazyLookupTable<string, long> { Depth = 2, LifeSpan = TimeSpan.FromSeconds(60) };
        IServiceHost _host;

        ProfileSqliteService _profileSql;
        SearchSqliteService _searchSql;
        ErrorReportsService _errorResport;

        bool _running;

        readonly object _updateLock = new object();
        List<AttachementDataTransaction> _updates = new List<AttachementDataTransaction>();

        readonly object _searchUpdateLock = new object();
        HashSet<long> _searchUpdates = new HashSet<long>();

        public void Initalize(ServiceOptions options)
        {
            options.EnableChainFeature(ChainType.Data, 0, AccountIndex.FeatureId);
        }

        public async Task<ServiceResult> Start(string configurationString, IServiceHost host)
        {
            _host = host;
            _running = true;

            var profileSchema = @"{
                  'type': 'array',
                  'additionalItems': false,
                  'items': {
                    'type': 'object',
                    'additionalProperties': false,
                    'required': ['k', 'v'],
                    'properties': {
                      'k': {
                        'type': 'string'
                      },
                      'v': {
                        'type': 'string'
                      },
                      'p': {
                        'type': ['string', 'null']
                      }
                    }
                  }
                }";

            _profileSchema = JSchema.Parse(profileSchema);

            var configuration = Service.ServiceHelper.GetConfiguration(configurationString);
            var dataPath = configuration[Service.ServiceHelper.ServiceDataPathKey];

            _profileSql = new ProfileSqliteService();
            await _profileSql.Init(dataPath);
            _profileSql.FillCommandPool(2);

            _searchSql = new SearchSqliteService();
            await _searchSql.Init(dataPath);

            _errorResport = new ErrorReportsService();
            await _errorResport.Init(dataPath);

            TaskRunner.Run(() => ProfileUpdateLoop());
            TaskRunner.Run(() => SearchUpdateLoop());

            return new ServiceResult(ServiceResultTypes.Ok, ProfileServiceInfo.Version, ProfileServiceInfo.Name);
        }

        public async Task Stop()
        {
            _running = false;

            await Task.Delay(1000);

            _profileSql?.Dispose();
            _profileSql = null;
            _searchSql?.Dispose();
            _searchSql = null;
            _errorResport?.Dispose();
            _errorResport = null;
        }

        static ServiceResult AreAttachementItemsValid(IReadOnlyList<AttachementItem> items, AttachementDataTransaction transaction)
        {
            if (items.Count == 0 || items.Count > 2)
                return new ServiceResult(ServiceResultTypes.False, (int)ProfileUserCodes.InvalidAttachements);

            var hasProfile = false;
            var hasImage = false;

            var userCode = ProfileUserCodes.Ok;

            foreach (var item in items)
            {
                if (item.Name == ProfileServiceInfo.ProfileJsonFileName)
                {
                    if (item.DataSize <= ProfileServiceInfo.ProfileJsonMaxFileSize)
                    {
                        if (hasProfile)
                        {
                            userCode = ProfileUserCodes.InvalidAttachements;
                            break;
                        }

                        hasProfile = true;
                        continue;
                    }

                    userCode = ProfileUserCodes.InvalidProfileJsonFizeSize;
                    break;
                }

                if (item.Name == ProfileServiceInfo.ImageFileName)
                {
                    if (item.DataSize <= ProfileServiceInfo.ImageMaxFileSize)
                    {
                        if (hasImage)
                        {
                            userCode = ProfileUserCodes.InvalidAttachements;
                            break;
                        }

                        hasImage = true;
                        continue;
                    }

                    userCode = ProfileUserCodes.InvalidImageFileSize;
                    break;
                }

                userCode = ProfileUserCodes.InvalidAttachements;
                break;
            }

            if (transaction != null)
            {
                var index = transaction.GetFeature<AccountIndex>(AccountIndex.FeatureId)?.Index;
                var validIndex = false;
                if (index != null)
                {
                    validIndex = (hasProfile && hasImage && index == ProfileServiceInfo.ProfileAndImageIndex) ||
                                    (hasProfile && !hasImage && index == ProfileServiceInfo.ProfileIndex) ||
                                    (!hasProfile && hasImage && index == ProfileServiceInfo.ImageIndex);
                }
                if (!validIndex)
                    userCode = ProfileUserCodes.InvalidTransaction;
            }

            var valid = (hasProfile || hasImage) && userCode == ProfileUserCodes.Ok;

            return new ServiceResult(valid ? ServiceResultTypes.Ok : ServiceResultTypes.False, (long)userCode);
        }

        public (string, string/*, string*/) GetProfileJsonNames(string filePath, bool validateJson)
        {
            string profileName = null;
            string realName = null;
            //string tagLine = null;

            try
            {
                var jsonText = File.ReadAllText(filePath);
                var array = JArray.Parse(jsonText);

                if (validateJson && !array.IsValid(_profileSchema))
                    return (null, null/*, null*/);

                foreach (var token in array)
                {
                    var obj = (JObject)token;
                    var prop = obj.GetValue("p").Value<string>();

                    /*
                    if(prop == ProfileJsonItem.BioItem)
                    {
                        tagLine = obj.GetValue("v").Value<string>();
                        if (!string.IsNullOrEmpty(realName) && !string.IsNullOrEmpty(profileName))
                            break;
                    }
                    */

                    if (prop == ProfileItemJson.ProfileNameItem)
                    {
                        profileName = obj.GetValue("v").Value<string>();
                        if (!string.IsNullOrEmpty(realName)/* && !string.IsNullOrEmpty(tagLine)*/)
                            break;
                    }

                    if (prop == ProfileItemJson.RealNameItem)
                    {
                        realName = obj.GetValue("v").Value<string>();
                        if (!string.IsNullOrEmpty(profileName)/* && !string.IsNullOrEmpty(tagLine)*/)
                            break;
                    }
                }
            }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
            {

            }

            return (profileName, realName/*, tagLine*/);
        }

        public ProfileUserCodes ValidateJson(string filePath, out string profileName, out string realName)
        {
            profileName = null;
            realName = null;

            try
            {
                (profileName, realName/*, _*/) = GetProfileJsonNames(filePath, true);
                if (!string.IsNullOrEmpty(profileName) && !string.IsNullOrEmpty(realName))
                    return ProfileUserCodes.Ok;
            }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
            {

            }
            return ProfileUserCodes.InvalidProfileJson;
        }

        public Task<ServiceResult> IsServiceTransactionValid(ServiceTransaction serviceTransaction)
        {
            if (serviceTransaction.TransactionType == ServiceTransactionTypes.Join)
                return Task.FromResult(ServiceResult.Ok);

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ProfileUserCodes.InvalidTransaction));
        }

        public Task<ServiceResult> IsDataTransactionValid(DataTransaction dataTransaction)
        {
            var type = dataTransaction.TransactionType;

            if (dataTransaction.PrivacyType == DataTransactionPrivacyType.PublicData)
            {
                if (type == DataTransactionTypes.Attachement)
                {
                    var cat = dataTransaction as AttachementDataTransaction;
                    return Task.FromResult(AreAttachementItemsValid(cat.Items, dataTransaction as AttachementDataTransaction));
                }
            }

            return Task.FromResult(new ServiceResult(ServiceResultTypes.False, (long)ProfileUserCodes.InvalidTransaction));
        }

        public async Task<ServiceResult> AreAttachementsValid(Attachements chainAttachements, List<ServiceAttachementFile> tempFiles)
        {
            var result = (tempFiles.Count > 0 && tempFiles.Count <= 2) ? ServiceResultTypes.Ok : ServiceResultTypes.False;
            var userCode = result == ServiceResultTypes.Ok ? ProfileUserCodes.Ok : ProfileUserCodes.InvalidAttachements;

            var profileName = string.Empty;
            var realName = string.Empty;

            foreach (var file in tempFiles)
            {
                if (file.Item.Name == ProfileServiceInfo.ProfileJsonFileName)
                {
                    var jsonResult = ValidateJson(file.TempPath, out profileName, out realName);
                    if (jsonResult == ProfileUserCodes.Ok)
                    {
                        if (!ProfileServiceInfo.IsRealNameValid(realName))
                        {
                            result = ServiceResultTypes.False;
                            userCode = ProfileUserCodes.InvalidRealName;
                            break;
                        }

                        if (!ProfileServiceInfo.IsProfileNameValid(profileName))
                        {
                            result = ServiceResultTypes.False;
                            userCode = ProfileUserCodes.InvalidProfileName;
                            break;
                        }
                    }
                    else
                    {
                        result = ServiceResultTypes.False;
                        userCode = ProfileUserCodes.InvalidProfileJson;

                        break;
                    }
                }
                else if (file.Item.Name == ProfileServiceInfo.ImageFileName)
                {
                    var imgResult = Image.IsValidImage(file.TempPath, ProfileServiceInfo.ImageMaxFileSize);
                    if (!imgResult.IsValid)
                    {
                        result = ServiceResultTypes.False;
                        userCode = ProfileUserCodes.InvalidImage;
                        if (imgResult.Result == ImageInfoResult.InvalidFileSize)
                            userCode = ProfileUserCodes.InvalidImageFileSize;

                        break;
                    }

                    if (imgResult.Width != imgResult.Height || imgResult.Height < 128 || imgResult.Height > ProfileServiceInfo.ImageMaxDimensions)
                    {
                        result = ServiceResultTypes.False;
                        userCode = ProfileUserCodes.InvalidImageDimensions;
                        break;
                    }
                }
                else
                {
                    result = ServiceResultTypes.False;
                    userCode = ProfileUserCodes.InvalidAttachements;
                    break;
                }
            }

            var checkAccountName = true;
            var profile = await _profileSql.GetProfile(chainAttachements.AccountId);
            if (profile != null)
            {
                checkAccountName = !(profileName == profile.ProfileName);
            }
            else
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    result = ServiceResultTypes.False;
                    userCode = ProfileUserCodes.InvalidProfileJson;

                    goto end;
                }
            }

            if (checkAccountName)
            {
                lock (_reserved)
                {
                    if (_reserved.TryGetValue(profileName, out var nameId))
                    {
                        if (nameId != chainAttachements.AccountId)
                        {
                            result = ServiceResultTypes.False;
                            userCode = ProfileUserCodes.ProfileNameInUse;
                            goto end;
                        }
                    }
                    else
                    {
                        _reserved.Add(profileName, chainAttachements.AccountId);
                    }
                }

                if (await _profileSql.ContainsProfile(profileName))
                {
                    result = ServiceResultTypes.False;
                    userCode = ProfileUserCodes.ProfileNameInUse;
                }
            }

        end:
            return new ServiceResult(result, (long)userCode);
        }

        public Task<ServiceResult> IsValidAttachementsRequest(Attachements attachements)
        {
            return Task.FromResult(AreAttachementItemsValid(attachements.Items, null));
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
                if (transaction.TransactionType == DataTransactionTypes.Attachement)
                {
                    lock (_updateLock)
                        _updates.Add(transaction as AttachementDataTransaction);
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

            if (segments.Length == 3)
            {
                if (segments[0] == "profileinfobyid" && long.TryParse(segments[1], out var accountId) && accountId > 0)
                {
                    return await _profileSql.GetProfile(accountId);
                }

                if (segments[0] == "search" && segments[1].Length >= ProfileServiceInfo.MinNameLength)
                {
                    return await _searchSql.Search(segments[1]);
                }
            }

            return null;
        }

        async Task ProfileUpdateLoop()
        {
            while (_running)
            {
                await Task.Delay(1000);

                List<AttachementDataTransaction> profileUpdates = null;
                lock (_updateLock)
                {
                    if (_updates.Count > 0)
                    {
                        profileUpdates = _updates;
                        _updates = new List<AttachementDataTransaction>();
                    }
                }

                if (profileUpdates != null)
                {
                    foreach (var att in profileUpdates)
                    {
                        string profileName = null;
                        string realName = null;

                        var profileTransactionId = 0L;
                        var imageTransactionId = 0L;
                        var attachementKey = att.AttachementKey;

                        try
                        {
                            foreach (var item in att.Items)
                            {
                                if (item.Name == ProfileServiceInfo.ProfileJsonFileName)
                                {
                                    profileTransactionId = att.TransactionId;

                                    ///string tagline = null;
                                    var path = _host.GetDataChain(0).GetLocalAttachementPath(att.TransactionId, att.AttachementKey, ProfileServiceInfo.ProfileJsonFileName);
                                    (profileName, realName/*, tagline*/) = GetProfileJsonNames(path, false);
                                }
                                else if (item.Name == ProfileServiceInfo.ImageFileName)
                                {
                                    imageTransactionId = att.TransactionId;
                                }
                            }

                            await _profileSql.UpdateProfile(att.AccountId, profileName, realName, profileTransactionId, imageTransactionId, attachementKey);
                        }
                        catch (Exception ex)
                        {
                            Log.HandleException(ex, this);
                        }

                        if (profileTransactionId > 0)
                        {
                            lock (_searchUpdateLock)
                                _searchUpdates.Add(att.AccountId);
                        }
                    }
                }
            }
        }

        async Task SearchUpdateLoop()
        {
            while (_running)
            {
                await Task.Delay(1000);

                HashSet<long> searchUpdates = null;
                lock (_searchUpdateLock)
                {
                    if (_searchUpdates.Count > 0)
                    {
                        searchUpdates = _searchUpdates;
                        _searchUpdates = new HashSet<long>();
                    }
                }

                if (searchUpdates != null)
                {
                    foreach (var accountId in searchUpdates)
                    {
                        try
                        {
                            var profile = await _profileSql.GetProfile(accountId);
                            if (profile != null)
                                await _searchSql.UpdateSearch(profile);
                        }
                        catch (Exception ex)
                        {
                            Log.HandleException(ex, this);
                        }
                    }

                    await _searchSql.Optimize();
                }
            }
        }
    }
}
