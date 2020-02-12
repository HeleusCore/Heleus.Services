using Microsoft.Data.Sqlite;
using Heleus.ServiceHelper.Sqlite;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Heleus.StatusService
{
    public enum StatusSqliteCommand
    {
        Version,
        BuildTable,
        Vacuum,

        InsertNewAccount,
        DeleteNewAccount,
        SelectNewAccounts,

        InsertRecentAccount,
        DeleteRecentAccount,
        SelectRecentAccounts,
        CleanupRecentAccounts,

        InsertPopularAccount,
        DeletePopularAccount,
        SelectPopularAccounts,
        CleanupPopularAccounts
    }

    public class StatusSqliteService : SqliteService<StatusSqliteCommand>
    {
        public StatusSqliteService() : base(StatusSqliteCommand.Version, StatusSqliteCommand.BuildTable, StatusSqliteCommand.Vacuum)
        {
        }

        protected override void RegisterCommands()
        {
            RegisterCommand(StatusSqliteCommand.BuildTable, (command) =>
            {
                command.CommandText =
                @"
                    BEGIN;

                    CREATE TABLE IF NOT EXISTS NEW (
                        ACCOUNTID INTEGER UNIQUE NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS RECENT (
                        ACCOUNTID INTEGER UNIQUE NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS POPULAR (
                        ACCOUNTID INTEGER UNIQUE NOT NULL
                    );

                    END;
                ";
            }, false);

            AddCrudCommands("NEW", StatusSqliteCommand.InsertNewAccount, StatusSqliteCommand.DeleteNewAccount, StatusSqliteCommand.SelectNewAccounts);
            AddCrudCommands("RECENT", StatusSqliteCommand.InsertRecentAccount, StatusSqliteCommand.DeleteRecentAccount, StatusSqliteCommand.SelectRecentAccounts);
            AddCrudCommands("POPULAR", StatusSqliteCommand.InsertPopularAccount, StatusSqliteCommand.DeletePopularAccount, StatusSqliteCommand.SelectPopularAccounts);
            AddCleanupCommand("RECENT", StatusSqliteCommand.CleanupRecentAccounts);
            AddCleanupCommand("POPULAR", StatusSqliteCommand.CleanupPopularAccounts);
        }

        void AddCrudCommands(string tableName, StatusSqliteCommand insert, StatusSqliteCommand delete, StatusSqliteCommand select )
        {
            RegisterCommand(insert, (command) =>
            {
                command.CommandText = $"INSERT INTO {tableName} (ACCOUNTID) VALUES ($ACCOUNTID)";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
            });

            RegisterCommand(delete, (command) =>
            {
                command.CommandText = $"DELETE FROM {tableName} WHERE ACCOUNTID = $ACCOUNTID;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
            });

            RegisterCommand(select, (command) =>
            {
                command.CommandText = $"SELECT ACCOUNTID FROM {tableName} ORDER BY ROWID DESC LIMIT $LIMIT";
                command.Parameters.Add("$LIMIT", SqliteType.Integer);
            });
        }

        void AddCleanupCommand(string tableName, StatusSqliteCommand deleteOldCommand)
        {
            RegisterCommand(deleteOldCommand, (command) =>
            {
                command.CommandText = $"DELETE FROM {tableName} WHERE ROWID IN (SELECT ROWID FROM {tableName} ORDER BY ROWID DESC LIMIT -1 OFFSET $OFFSET)";
                command.Parameters.Add("$OFFSET", SqliteType.Integer);
            });
        }

        async Task<bool> InsertAccount(StatusSqliteCommand insertCommand, long accountId)
        {
            using (var pool = GetCommandPool(insertCommand))
            {
                var command = pool.GetCommand();
                command.Parameters["$ACCOUNTID"].Value = accountId;

                try
                {
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
                catch { }
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

                return false;
            }
        }

        async Task<bool> DeleteAccount(StatusSqliteCommand deleteCommand, long accountId)
        {
            return await ExecuteNoneQuery(deleteCommand, (command) =>
            {
                command.Parameters["$ACCOUNTID"].Value = accountId;
            }) > 0;
        }

        async Task<List<long>> SelectAccounts(StatusSqliteCommand selectCommand, int limit)
        {
            var result = new List<long>(limit);

            using (var pool = GetCommandPool(selectCommand))
            {
                var command = pool.GetCommand();
                command.Parameters["$LIMIT"].Value = limit;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        result.Add(reader.GetInt64(0));
                    }
                }
            }

            return result;
        }

        async Task<bool> CleanupAccounts(StatusSqliteCommand cleanupCommand, int offset)
        {
            return await ExecuteNoneQuery(cleanupCommand, (command) =>
            {
                command.Parameters["$OFFSET"].Value = offset;
            }) > 0;
        }

        public Task<bool> InsertNewAccount(long accountId)
        {
            return InsertAccount(StatusSqliteCommand.InsertNewAccount, accountId);
        }

        public Task<bool> DeleteNewAccount(long accountId)
        {
            return DeleteAccount(StatusSqliteCommand.DeleteNewAccount, accountId);
        }

        public Task<List<long>> SelectNewAccounts(int limit)
        {
            return SelectAccounts(StatusSqliteCommand.SelectNewAccounts, limit);
        }

        public Task<bool> InsertRecentAccount(long accountId)
        {
            return InsertAccount(StatusSqliteCommand.InsertRecentAccount, accountId);
        }

        public Task<bool> DeleteRecentAccount(long accountId)
        {
            return DeleteAccount(StatusSqliteCommand.DeleteRecentAccount, accountId);
        }

        public Task<List<long>> SelectRecentAccounts(int limit)
        {
            return SelectAccounts(StatusSqliteCommand.SelectRecentAccounts, limit);
        }

        public Task<bool> CleanupRecentAccounts(int offset)
        {
            return CleanupAccounts(StatusSqliteCommand.CleanupRecentAccounts, offset);
        }

        public Task<bool> InsertPopularAccount(long accountId)
        {
            return InsertAccount(StatusSqliteCommand.InsertPopularAccount, accountId);
        }

        public Task<bool> DeletePopularAccount(long accountId)
        {
            return DeleteAccount(StatusSqliteCommand.DeletePopularAccount, accountId);
        }

        public Task<List<long>> SelectPopularAccounts(int limit)
        {
            return SelectAccounts(StatusSqliteCommand.SelectPopularAccounts, limit);
        }

        public Task<bool> CleanupPopularAccounts(int offset)
        {
            return CleanupAccounts(StatusSqliteCommand.CleanupPopularAccounts, offset);
        }
    }
}
