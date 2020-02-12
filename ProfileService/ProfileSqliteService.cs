using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Heleus.Operations;
using Heleus.ServiceHelper.Sqlite;

namespace Heleus.ProfileService
{
    public enum ProfileSqliteCommand
    {
        Version,
        BuildTable,
        Vaccum,
        IdExists,
        NameExists,
        ProfileById,
        ProfileByName,
        UpdateImage,
        UpdateName,
        UpdateAll
    }

    public class ProfileSqliteService : SqliteService<ProfileSqliteCommand>
    {
        public ProfileSqliteService() : base(ProfileSqliteCommand.Version, ProfileSqliteCommand.BuildTable, ProfileSqliteCommand.Vaccum)
        {
        }

        protected override void RegisterCommands()
        {
            RegisterCommand(ProfileSqliteCommand.BuildTable, (command) =>
            {
                command.CommandText =
                    @"
                    BEGIN;

                    CREATE TABLE IF NOT EXISTS PROFILES(
                        ACCOUNTID INTEGER PRIMARY KEY,
                        PROFILENAME TEXT NOT NULL COLLATE BINARY,
                        REALNAME TEXT NOT NULL,
                        JSONID INTEGER NOT NULL,
                        JSONATTACHEMENTKEY INTEGER NOT NULL,
                        IMAGEID INTEGER NOT NULL,
                        IMAGEATTACHEMENTKEY INTEGER NOT NULL
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS PROFILENAME_INDEX ON PROFILES(PROFILENAME);

                    END;";
            }, false);

            RegisterCommand(ProfileSqliteCommand.ProfileById, (command) =>
            {
                command.CommandText = "SELECT * FROM PROFILES WHERE ACCOUNTID = $ACCOUNTID;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
            });

            RegisterCommand(ProfileSqliteCommand.ProfileByName, (command) =>
            {
                command.CommandText = "SELECT * FROM PROFILES WHERE PROFILENAME = $PROFILENAME;";
                command.Parameters.Add("$PROFILENAME", SqliteType.Text);
            });

            RegisterCommand(ProfileSqliteCommand.IdExists, (command) =>
            {
                command.CommandText = "SELECT COUNT(*) FROM PROFILES WHERE ACCOUNTID = $ACCOUNTID;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
            });

            RegisterCommand(ProfileSqliteCommand.NameExists, (command) =>
            {
                command.CommandText = "SELECT COUNT(*) FROM PROFILES WHERE PROFILENAME = $PROFILENAME;";
                command.Parameters.Add("$PROFILENAME", SqliteType.Text);
            });

            RegisterCommand(ProfileSqliteCommand.UpdateImage, (command) =>
            {
                command.CommandText = "UPDATE PROFILES SET IMAGEID = $IMAGEID, IMAGEATTACHEMENTKEY = $IMAGEATTACHEMENTKEY WHERE ACCOUNTID = $ACCOUNTID;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$IMAGEID", SqliteType.Integer);
                command.Parameters.Add("$IMAGEATTACHEMENTKEY", SqliteType.Integer);
            });

            RegisterCommand(ProfileSqliteCommand.UpdateName, (command) =>
            {
                // upsert https://www.sqlite.org/lang_UPSERT.html
                command.CommandText = "INSERT INTO PROFILES VALUES ($ACCOUNTID, $PROFILENAME, $REALNAME, $JSONID, $JSONATTACHEMENTKEY, 0, 0) ON CONFLICT(ACCOUNTID) DO UPDATE SET PROFILENAME = $PROFILENAME, REALNAME = $REALNAME, JSONID = $JSONID, JSONATTACHEMENTKEY = $JSONATTACHEMENTKEY, IMAGEID = IMAGEID, IMAGEATTACHEMENTKEY = IMAGEATTACHEMENTKEY;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$PROFILENAME", SqliteType.Text);
                command.Parameters.Add("$REALNAME", SqliteType.Text);
                command.Parameters.Add("$JSONID", SqliteType.Integer);
                command.Parameters.Add("$JSONATTACHEMENTKEY", SqliteType.Integer);
            });

            RegisterCommand(ProfileSqliteCommand.UpdateAll, (command) =>
            {
                command.CommandText = "INSERT INTO PROFILES VALUES ($ACCOUNTID, $PROFILENAME, $REALNAME, $JSONID, $JSONATTACHEMENTKEY, $IMAGEID, $IMAGEATTACHEMENTKEY) ON CONFLICT(ACCOUNTID) DO UPDATE SET PROFILENAME = $PROFILENAME, REALNAME = $REALNAME, JSONID = $JSONID, JSONATTACHEMENTKEY = $JSONATTACHEMENTKEY, IMAGEID = $IMAGEID, IMAGEATTACHEMENTKEY = $IMAGEATTACHEMENTKEY;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$PROFILENAME", SqliteType.Text);
                command.Parameters.Add("$REALNAME", SqliteType.Text);
                command.Parameters.Add("$JSONID", SqliteType.Integer);
                command.Parameters.Add("$JSONATTACHEMENTKEY", SqliteType.Integer);
                command.Parameters.Add("$IMAGEID", SqliteType.Integer);
                command.Parameters.Add("$IMAGEATTACHEMENTKEY", SqliteType.Integer);
            });
        }

        public async Task<bool> ContainsProfile(long accountId)
        {
            using (var pool = GetCommandPool(ProfileSqliteCommand.IdExists))
            {
                var command = pool.GetCommand();
                command.Parameters["$ACCOUNTID"].Value = accountId;

                return (long)await command.ExecuteScalarAsync() > 0;
            }
        }

        public async Task<bool> ContainsProfile(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > ProfileServiceInfo.MaxNameLength)
                return false;

            using (var pool = GetCommandPool(ProfileSqliteCommand.NameExists))
            {
                var command = pool.GetCommand();
                command.Parameters["$PROFILENAME"].Value = name;

                return (long)await command.ExecuteScalarAsync() > 0;
            }
        }

        public async Task<ProfileInfo> GetProfile(long accountId)
        {
            using (var pool = GetCommandPool(ProfileSqliteCommand.ProfileById))
            {
                var command = pool.GetCommand();
                command.Parameters["$ACCOUNTID"].Value = accountId;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!reader.Read())
                        return null;

                    var profileName = reader.GetString(1);
                    var realName = reader.GetString(2);
                    var jid = reader.GetInt64(3);
                    var jattachementkey = reader.GetInt32(4);
                    var iid = reader.GetInt64(5);
                    var iattachementkey = reader.GetInt32(6);

                    return new ProfileInfo(accountId, profileName, realName, jid, jattachementkey, iid, iattachementkey);
                }
            }
        }

        public async Task<ProfileInfo> GetProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName) || profileName.Length > ProfileServiceInfo.MaxNameLength)
                return null;

            using (var pool = GetCommandPool(ProfileSqliteCommand.ProfileByName))
            {
                var command = pool.GetCommand();
                command.Parameters["$PROFILENAME"].Value = profileName;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!reader.Read())
                        return null;

                    var id = reader.GetInt64(0);
                    var dbProfileName = reader.GetString(1);
                    var realName = reader.GetString(2);
                    var jid = reader.GetInt64(3);
                    var jattachementkey = reader.GetInt32(4);
                    var iid = reader.GetInt64(5);
                    var iattachementkey = reader.GetInt32(6);

                    return new ProfileInfo(id, dbProfileName, realName, jid, jattachementkey, iid, iattachementkey);
                }
            }
        }

        public async Task<bool> UpdateProfile(long accountId, string profileName, string realName, long profileTransactionId, long imageTransactionId, int attachementKey)
        {
            var updateName = profileTransactionId >= Operation.FirstTransactionId && ProfileServiceInfo.IsProfileNameValid(profileName) && ProfileServiceInfo.IsRealNameValid(realName);
            var updateImage = imageTransactionId >= Operation.FirstTransactionId;

            if (updateName)
            {
                if (!updateImage)
                {
                    using (var pool = GetCommandPool(ProfileSqliteCommand.UpdateName))
                    {
                        var command = pool.GetCommand();
                        command.Parameters["$ACCOUNTID"].Value = accountId;
                        command.Parameters["$PROFILENAME"].Value = profileName;
                        command.Parameters["$REALNAME"].Value = realName;
                        command.Parameters["$JSONID"].Value = profileTransactionId;
                        command.Parameters["$JSONATTACHEMENTKEY"].Value = attachementKey;

                        return await command.ExecuteNonQueryAsync() > 0;
                    }
                }

                using (var pool = GetCommandPool(ProfileSqliteCommand.UpdateAll))
                {
                    var command = pool.GetCommand();
                    command.Parameters["$ACCOUNTID"].Value = accountId;
                    command.Parameters["$PROFILENAME"].Value = profileName;
                    command.Parameters["$REALNAME"].Value = realName;
                    command.Parameters["$JSONID"].Value = profileTransactionId;
                    command.Parameters["$JSONATTACHEMENTKEY"].Value = attachementKey;
                    command.Parameters["$IMAGEID"].Value = imageTransactionId;
                    command.Parameters["$IMAGEATTACHEMENTKEY"].Value = attachementKey;
                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }

            if (updateImage && !updateName)
            {
                using (var pool = GetCommandPool(ProfileSqliteCommand.UpdateImage))
                {
                    var command = pool.GetCommand();
                    command.Parameters["$ACCOUNTID"].Value = accountId;
                    command.Parameters["$IMAGEID"].Value = imageTransactionId;
                    command.Parameters["$IMAGEATTACHEMENTKEY"].Value = attachementKey;

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }

            return false;
        }
    }
}
