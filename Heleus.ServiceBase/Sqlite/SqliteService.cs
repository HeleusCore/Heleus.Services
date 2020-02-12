using System;
using Microsoft.Data.Sqlite;
using Heleus.Base;
using System.Threading.Tasks;
using System.IO;

namespace Heleus.ServiceHelper.Sqlite
{
    public enum ErrorReportSqliteCommands
    {
        Version,
        BuildTable,
        Vaccum,

        InsertMessage,
        SelectMessageErrorCount,
        UpdateMessageErrorCount,
        InsertReport
    }

    public abstract class SqliteSerivce
    {
        protected static bool _versionShown;
    }

    public abstract class SqliteService<Key> : SqliteSerivce, IDisposable, ILogger where Key : struct
    {
        public class SqliteReader : IDisposable
        {
            public readonly SqliteDataReader DataReader;
            readonly SqliteCommandPool<Key>.SqliteCommandPoolItem _item;

            public SqliteReader(SqliteCommandPool<Key>.SqliteCommandPoolItem item, SqliteDataReader reader)
            {
                DataReader = reader;
                _item = item;
            }

            public void Dispose()
            {
                DataReader.Dispose();
                _item.Dispose();
            }
        }

        public string LogName => GetType().Name;

        SqliteCommandPool<Key> _commandPool;

        readonly Key _versionKey;
        readonly Key _buildTableKey;
        readonly Key _vacuumKey;

        protected SqliteService(Key versionKey, Key buildTableKey, Key vacuumKey)
        {
            _versionKey = versionKey;
            _buildTableKey = buildTableKey;
            _vacuumKey = vacuumKey;
        }

        protected SqliteCommandPool<Key>.SqliteCommandPoolItem GetCommandPool(Key sqlCommandType)
        {
            return _commandPool.GetPoolItem(sqlCommandType);
        }

        protected void RegisterCommand(Key sqlCommandType, Action<SqliteCommand> process, bool prepare = true)
        {
            _commandPool.AddSqliteCommand(sqlCommandType, process, prepare);
        }

        public void FillCommandPool(int commandCount)
        {
            _commandPool.FillCommandPool(commandCount);
        }

        public virtual async Task Init(string dataPath)
        {
            var connection = new SqliteConnection($"Data Source={Path.Combine(dataPath, $"{GetType().Name.ToLower()}.db")}");
            await connection.OpenAsync();

            _commandPool = new SqliteCommandPool<Key>(connection);

            RegisterCommand(_versionKey, (command) =>
            {
                command.CommandText = "SELECT sqlite_version();";
            }, false);

            RegisterCommand(_vacuumKey, (command) =>
            {
                command.CommandText = "VACUUM;";
            }, true);

            /*
            RegisterCommand(_lastRowIdKey, (command) =>
            {
                command.CommandText = "SELECT last_insert_rowid();";
            }, true);
            */

            RegisterCommands();

            await BuildTable();
            await Vacuum();

            FillCommandPool(1);
        }

        protected abstract void RegisterCommands();

        Task BuildTable()
        {
            return BuildTable(_versionKey, _buildTableKey);
        }

        protected virtual async Task BuildTable(Key versionKey, Key buildTableKey)
        {
            using (var pool = GetCommandPool(versionKey))
            {
                if (!_versionShown)
                {
                    _versionShown = true; // print it only once

                    using (var reader = await pool.GetCommand().ExecuteReaderAsync())
                    {
                        reader.Read();
                        var version = reader.GetString(0);

                        Log.Info($"SQLite version: {version}", this);
                    }
                }
            }

            using (var pool = GetCommandPool(buildTableKey))
            {
                await pool.GetCommand().ExecuteNonQueryAsync();
            }
        }

        public async Task<object> ExecuteScalar(Key key, Action<SqliteCommand> updateCommand)
        {
            try
            {
                using (var pool = GetCommandPool(key))
                {
                    var command = pool.GetCommand();
                    updateCommand?.Invoke(command);

                    return await command.ExecuteScalarAsync();
                }
            }
            catch(Exception ex)
            {
                Log.HandleException(ex, this);
            }

            return null;
        }

        public async Task<long> ExecuteCount(Key key, Action<SqliteCommand> updateCommand)
        {
            var result = await ExecuteScalar(key, updateCommand);
            if (result == null)
                return 0;

            return (long)result;
        }

        public async Task<int> ExecuteNoneQuery(Key key, Action<SqliteCommand> updateCommand)
        {
            try
            {
                using (var pool = GetCommandPool(key))
                {
                    var command = pool.GetCommand();
                    updateCommand?.Invoke(command);

                    return await command.ExecuteNonQueryAsync();
                }
            }
            catch(Exception ex)
            {
                Log.HandleException(ex, this);
            }

            return 0;
        }

        public async Task<SqliteDataReader> ExecuteQuery(Key key, Action<SqliteCommand> updateCommand)
        {
            try
            {
                using (var pool = GetCommandPool(key))
                {
                    var command = pool.GetCommand();
                    updateCommand?.Invoke(command);

                    return await command.ExecuteReaderAsync();
                }
            }
            catch(Exception ex)
            {
                Log.HandleException(ex, this);
            }

            return null;
        }

        public async Task<SqliteReader> GetReader(Key key, Action<SqliteCommand> queryCommand)
        {
            var pool = GetCommandPool(key);

            try
            {
                var command = pool.GetCommand();
                queryCommand?.Invoke(command);

                var reader = await command.ExecuteReaderAsync();

                return new SqliteReader(pool, reader);
            }
            catch
            {
                pool.Dispose();
            }

            return null;
        }

        public async Task Vacuum()
        {
            using (var pool = GetCommandPool(_vacuumKey))
            {
                await pool.GetCommand().ExecuteNonQueryAsync();
            }
        }

        ~SqliteService()
        {
            Dispose();
        }

        public virtual void Dispose()
        {
            _commandPool.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
