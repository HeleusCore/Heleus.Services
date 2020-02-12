using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Heleus.ServiceHelper.Sqlite;
using Heleus.Base;
using System.Text;
using System.Collections.Generic;

namespace Heleus.ProfileService
{
    public enum SearchSqliteCommand
    {
        Version,
        BuildTable,
        Vacuum,
        Check,
        Insert,
        Update,
        Search,
        Optimize
    }

    public class SearchSqliteService : SqliteService<SearchSqliteCommand>
    {
        public SearchSqliteService() : base(SearchSqliteCommand.Version, SearchSqliteCommand.BuildTable, SearchSqliteCommand.Vacuum)
        {
        }

        protected override void RegisterCommands()
        {
            RegisterCommand(SearchSqliteCommand.BuildTable, (command) =>
            {
                command.CommandText = "CREATE VIRTUAL TABLE IF NOT EXISTS SEARCH USING fts5(PROFILEINFO UNINDEXED, SEARCHTEXT);";
            }, false);

            RegisterCommand(SearchSqliteCommand.Check, (command) =>
            {
                command.CommandText = "SELECT COUNT(*) FROM SEARCH WHERE ROWID = $ACCOUNTID;";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
            });

            RegisterCommand(SearchSqliteCommand.Insert, (command) =>
            {
                command.CommandText = "INSERT INTO SEARCH (ROWID, PROFILEINFO, SEARCHTEXT) VALUES ($ACCOUNTID, $PROFILEINFO, $SEARCHTEXT)";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$PROFILEINFO", SqliteType.Text);
                command.Parameters.Add("$SEARCHTEXT", SqliteType.Text);
            });

            RegisterCommand(SearchSqliteCommand.Update, (command) =>
            {
                command.CommandText = "UPDATE SEARCH SET PROFILEINFO = $PROFILEINFO, SEARCHTEXT = $SEARCHTEXT WHERE ROWID = $ACCOUNTID";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$PROFILEINFO", SqliteType.Text);
                command.Parameters.Add("$SEARCHTEXT", SqliteType.Text);
            });

            RegisterCommand(SearchSqliteCommand.Search, (command) =>
            {
                command.CommandText = "SELECT PROFILEINFO FROM SEARCH WHERE SEARCHTEXT MATCH $SEARCH ORDER BY RANK LIMIT 25";
                command.Parameters.Add("$SEARCH", SqliteType.Text);
            });

            RegisterCommand(SearchSqliteCommand.Optimize, (command) =>
            {
                command.CommandText = "INSERT INTO SEARCH(SEARCH) VALUES('optimize');";
            });
        }

        public async Task<bool> Optimize()
        {
            using(var pool = GetCommandPool(SearchSqliteCommand.Optimize))
            {
                var command = pool.GetCommand();

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> UpdateSearch(ProfileInfo profileInfo)
        {
            var available = false;

            using(var pool = GetCommandPool(SearchSqliteCommand.Check))
            {
                var command = pool.GetCommand();
                command.Parameters["$ACCOUNTID"].Value = profileInfo.AccountId;

                var result = await command.ExecuteScalarAsync();
                available = (long)result > 0;
            }

            var searchText = $"{profileInfo.ProfileName} {profileInfo.RealName}";
            var profileJson = Newtonsoft.Json.JsonConvert.SerializeObject(new ProfileInfoJson(profileInfo));

            if (!available)
            {
                using(var pool = GetCommandPool(SearchSqliteCommand.Insert))
                {
                    var command = pool.GetCommand();
                    command.Parameters["$ACCOUNTID"].Value = profileInfo.AccountId;
                    command.Parameters["$PROFILEINFO"].Value = profileJson;
                    command.Parameters["$SEARCHTEXT"].Value = searchText;

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }

            using (var pool = GetCommandPool(SearchSqliteCommand.Update))
            {
                var command = pool.GetCommand();
                command.Parameters["$ACCOUNTID"].Value = profileInfo.AccountId;
                command.Parameters["$PROFILEINFO"].Value = profileJson;
                command.Parameters["$SEARCHTEXT"].Value = searchText;

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<SearchResult> Search(string searchText)
        {
            if (searchText == null)
                return null;

            var count = searchText.Length;
            var search = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                var c = searchText[i];
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                    continue;

                search.Append(c);
            }

            if (search.Length < ProfileServiceInfo.MinSearchLength)
                return null;

            search.Append('*');
            var searchString = search.ToString();

            using (var pool = GetCommandPool(SearchSqliteCommand.Search))
            {
                var command = pool.GetCommand();
                command.Parameters["$SEARCH"].Value = searchString;

                //Log.Write($"Search Result for {searchText}, actual search {searchString}.");
                var result = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var profileInfo = reader.GetString(0);
                        result.Add(profileInfo);

                        //Log.Write($"ProfileInfo {profileInfo}", this);
                    }
                }

                return new SearchResult(result);
            }
        }
    }
}
