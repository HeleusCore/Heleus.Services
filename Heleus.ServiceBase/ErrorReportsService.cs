using System;
using Microsoft.Data.Sqlite;
using Heleus.Base;
using System.Threading.Tasks;
using System.Collections.Generic;
using Heleus.ServiceHelper.Sqlite;

namespace Heleus.ServiceHelper
{
    public sealed class ErrorReportsService : SqliteService<ErrorReportSqliteCommands>
    {
        bool _running;
        readonly object _lock = new object();
        List<Tuple<long, byte[]>> _reports = new List<Tuple<long, byte[]>>();

        public ErrorReportsService() : base(ErrorReportSqliteCommands.Version, ErrorReportSqliteCommands.BuildTable, ErrorReportSqliteCommands.Vaccum)
        {
        }

        protected override void RegisterCommands()
        {
            RegisterCommand(ErrorReportSqliteCommands.BuildTable, (command) =>
            {
                command.CommandText =
                @"
                    BEGIN;

                        CREATE TABLE IF NOT EXISTS MESSAGES(
                            MESSAGE TEXT NOT NULL,
                            VERSION INTEGER NOT NULL,
                            HASH INTEGER UNIQUE NOT NULL,
                            TIMESTAMP INTEGER NOT NULL,
                            ERRORCOUNT INTEGER NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS REPORTS(
                            ACCOUNTID INTEGER NOT NULL,
                            VERSION TEXT NOT NULL,
                            LANGUAGE TEXT NOT NULL,
                            PLATFORM TEXT NOT NULL,
                            DEVICE TEXT NOT NULL,
                            HASH INTEGER NOT NULL
                        );

                    END;
                ";
            }, false);

            RegisterCommand(ErrorReportSqliteCommands.InsertReport, (command) =>
            {
                command.CommandText = "INSERT INTO REPORTS VALUES ($ACCOUNTID, $VERSION, $LANGUAGE, $PLATFORM, $DEVICE, $HASH);";
                command.Parameters.Add("$ACCOUNTID", SqliteType.Integer);
                command.Parameters.Add("$VERSION", SqliteType.Text);
                command.Parameters.Add("$LANGUAGE", SqliteType.Text);
                command.Parameters.Add("$PLATFORM", SqliteType.Text);
                command.Parameters.Add("$DEVICE", SqliteType.Text);
                command.Parameters.Add("$HASH", SqliteType.Integer);
            });

            RegisterCommand(ErrorReportSqliteCommands.SelectMessageErrorCount, (command) =>
            {
                command.CommandText = "SELECT ERRORCOUNT FROM MESSAGES WHERE HASH = $HASH;";
                command.Parameters.Add("$HASH", SqliteType.Integer);
            });

            RegisterCommand(ErrorReportSqliteCommands.InsertMessage, (command) =>
            {
                command.CommandText = "INSERT INTO MESSAGES VALUES ($MESSAGE, $VERSION, $HASH, $TIMESTAMP, 1);";
                command.Parameters.Add("$MESSAGE", SqliteType.Text);
                command.Parameters.Add("$VERSION", SqliteType.Text);
                command.Parameters.Add("$HASH", SqliteType.Integer);
                command.Parameters.Add("$TIMESTAMP", SqliteType.Integer);
            });

            RegisterCommand(ErrorReportSqliteCommands.UpdateMessageErrorCount, (command) =>
            {
                command.CommandText = "UPDATE MESSAGES SET ERRORCOUNT = ERRORCOUNT + 1 WHERE HASH = $HASH;";
                command.Parameters.Add("$HASH", SqliteType.Integer);
            });
        }

        public void QueueErrorReports(long accountId, byte[] errorReports)
        {
            lock (_lock)
                _reports.Add(Tuple.Create(accountId, errorReports));
        }

        public override async Task Init(string dataPath)
        {
            await base.Init(dataPath);
            _running = true;
            TaskRunner.Run(() => Loop());
        }

        async Task Loop()
        {
            while (_running)
            {
                List<Tuple<long, byte[]>> reports = null;
                lock (_lock)
                {
                    if (_reports.Count > 0)
                    {
                        reports = _reports;
                        _reports = new List<Tuple<long, byte[]>>();
                    }
                }

                if (reports != null)
                {
                    foreach (var reportItem in reports)
                    {
                        try
                        {
                            var accountId = reportItem.Item1;
                            var reportData = reportItem.Item2;

                            var errorReports = Service.ServiceHelper.GerErrorReports(reportData);
                            foreach (var report in errorReports)
                            {
                                if (report.Valid)
                                {
                                    var hash = report.Version.GetHashCode() + report.Message.GetHashCode();
                                    var count = await ExecuteCount(ErrorReportSqliteCommands.SelectMessageErrorCount, (command) =>
                                    {
                                        command.Parameters["$HASH"].Value = hash;
                                    });

                                    if (count <= 0)
                                    {
                                        await ExecuteNoneQuery(ErrorReportSqliteCommands.InsertMessage, (command) =>
                                        {
                                            command.Parameters["$MESSAGE"].Value = report.Message;
                                            command.Parameters["$VERSION"].Value = report.Version;
                                            command.Parameters["$HASH"].Value = hash;
                                            command.Parameters["$TIMESTAMP"].Value = Math.Min(Time.Timestamp, report.TimeStamp);
                                        });
                                    }

                                    await ExecuteNoneQuery(ErrorReportSqliteCommands.InsertReport, (command) =>
                                    {
                                        command.Parameters["$ACCOUNTID"].Value = accountId;
                                        command.Parameters["$VERSION"].Value = report.Version;
                                        command.Parameters["$LANGUAGE"].Value = report.Language;
                                        command.Parameters["$PLATFORM"].Value = report.Platform;
                                        command.Parameters["$DEVICE"].Value = report.Device;
                                        command.Parameters["$HASH"].Value = report.Hash;
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }

                await Task.Delay(2500);
            }
        }

        public override void Dispose()
        {
            _running = false;
            base.Dispose();
        }
    }
}
