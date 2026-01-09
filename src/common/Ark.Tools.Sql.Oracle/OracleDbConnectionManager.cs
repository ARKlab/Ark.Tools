// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Oracle.ManagedDataAccess.Client;

using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.Oracle;

public class OracleDbConnectionManager : IDbConnectionManager
{
    protected static void OnInfoMessage(object sender, OracleInfoMessageEventArgs ev)
    {
        OracleExceptionHandler.LogSqlInfoMessage(ev);
    }


    public DbConnection Get(string connectionString)
    {
        var conn = Build(connectionString);
        try
        {
            conn.Open();
            return conn;
        }
        catch
        {
            conn?.Dispose();
            throw;
        }
    }

    public async Task<DbConnection> GetAsync(string connectionString, CancellationToken ctk = default)
    {
        var conn = Build(connectionString);
        try
        {
            await conn.OpenAsync(ctk).ConfigureAwait(false);
            return conn;
        }
        catch
        {
            conn?.Dispose();
            throw;
        }
    }

    protected virtual OracleConnection Build(string connectionString)
    {
        var conn = new OracleConnection(connectionString);

        conn.InfoMessage += new OracleInfoMessageEventHandler(OnInfoMessage);

        return conn;
    }
}
