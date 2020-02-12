using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Heleus.ServiceHelper.Sqlite
{
    public class SqliteCommandPool<Key> : IDisposable where Key : struct
    {
        readonly object _lock = new object();
        SqliteConnection _connection;

        class CommandData
        {
            public CommandData(Action<SqliteCommand> process, bool prepare)
            {
                Process = process;
                Prepare = prepare;
            }

            public readonly bool Prepare;
            public readonly Action<SqliteCommand> Process;
        }

        readonly Dictionary<Key, CommandData> _commands = new Dictionary<Key, CommandData>();
        readonly Dictionary<Key, Stack<SqliteCommand>> _commandPool = new Dictionary<Key, Stack<SqliteCommand>>();

        public SqliteCommandPool(SqliteConnection connection)
        {
            _connection = connection;
        }

        public void FillCommandPool(int commandCount)
        {
            foreach(var command in _commands)
            {
                if (command.Value.Prepare)
                {
                    using (var pool = GetPoolItem(command.Key))
                    {
                        for (var i = 0; i < commandCount; i++)
                        {
                            pool.GetCommand();
                        }
                    }
                }
            }
        }

        public void AddSqliteCommand(Key key, Action<SqliteCommand> process, bool prepare = true)
        {
            if (process != null)
            {
                if (_commands.ContainsKey(key))
                    throw new ArgumentException("Key already added.", nameof(key));
                _commands[key] = new CommandData(process, prepare);
            }
        }

        SqliteCommand NewCommand(Key key)
        {
            lock (_lock)
            {
                if (_commandPool.TryGetValue(key, out var stack))
                {
                    if (stack.Count > 0)
                        return stack.Pop();
                }
            }

            var commandData = _commands[key];
            var command = _connection.CreateCommand();
            commandData.Process.Invoke(command);
            if(commandData.Prepare)
                command.Prepare();

            return command;
        }

        void ReleaseCommands(SqliteCommandPoolItem poolItem)
        {
            lock (_lock)
            {
                if (!_commandPool.TryGetValue(poolItem._key, out var stack))
                {
                    stack = new Stack<SqliteCommand>();
                    _commandPool[poolItem._key] = stack;
                }

                foreach (var client in poolItem._commands)
                    stack.Push(client);
            }
        }

        public SqliteCommandPoolItem GetPoolItem(Key key)
        {
            return new SqliteCommandPoolItem(key, this);
        }

        ~SqliteCommandPool()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            foreach(var commands in _commandPool.Values)
            {
                foreach(var command in commands)
                {
                    command.Dispose();
                }
                commands.Clear();
            }

            GC.SuppressFinalize(this);
        }

        public class SqliteCommandPoolItem : IDisposable
        {
            readonly internal Key _key;
            readonly SqliteCommandPool<Key> _pool;
            readonly internal List<SqliteCommand> _commands = new List<SqliteCommand>();

            internal SqliteCommandPoolItem(Key key, SqliteCommandPool<Key> pool)
            {
                _key = key;
                _pool = pool;
            }

            public SqliteCommand GetCommand()
            {
                var command = _pool.NewCommand(_key);
                _commands.Add(command);
                return command;
            }

            ~SqliteCommandPoolItem()
            {
                Dispose();
            }

            public void Dispose()
            {
                _pool.ReleaseCommands(this);
                _commands.Clear();
                GC.SuppressFinalize(this);
            }
        }
    }
}
