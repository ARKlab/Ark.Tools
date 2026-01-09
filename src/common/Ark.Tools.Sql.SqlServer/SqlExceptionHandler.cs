// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Data.SqlClient;

using NLog;

using System;

namespace Ark.Tools.Sql.SqlServer;

public static class SqlExceptionHandler
{
    public static void LogSqlException(SqlException exception)
    {
        if (exception != null)
        {
            LogSqlErrorCollection(exception.Errors);
        }
    }

    public static void LogSqlInfoMessage(SqlInfoMessageEventArgs ev)
    {
        if (ev != null)
        {
            LogSqlErrorCollection(ev.Errors);
        }
    }

    public static void LogSqlErrorCollection(SqlErrorCollection errors)
    {
        for (int i = 0; i < errors.Count; i++)
        {
            LogSqlError(errors[i]);
        }
    }

    public static bool IsPrimaryKeyOrUniqueKeyViolation(SqlException ex)
    {
        return ex.Class == 14 && (ex.Number == 2627 || ex.Number == 2601);
    }

    public static void LogSqlError(SqlError e)
    {
        if (e != null)
        {
            Logger logger = LogManager.GetLogger(e.Procedure + "@" + e.Server);
            var logMessage = e.Message;
            var logException = new InvalidOperationException("Exception at Line: " + e.LineNumber);

            switch (e.Class)
            {
                case 0:
                case 1: // Trace: RAISERROR('Trace Message', 1,  1) WITH NOWAIT; 
                    logger.Trace(logException, logMessage);
                    break;
                case 2: // Debug: RAISERROR('Debug Message', 2,  1) WITH NOWAIT;
                    logger.Debug(logException, logMessage);
                    break;
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                    logger.Info(logException, logMessage);
                    break;
                case 11:
                case 12:
                case 13:
                case 14:
                case 15:
                case 16: // Error RAISERROR('Error Message', 16,  1) WITH NOWAIT; 
                case 17:
                case 18:
                case 19: // Error that users can't RAISE
                    logger.Error(logException, logMessage);
                    break;
                case 20: // from here the connection is forcefull closed
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                default: // from 17 to 24 or unknown
                    logger.Fatal(logException, logMessage);
                    break;


            }



        }
    }
}
