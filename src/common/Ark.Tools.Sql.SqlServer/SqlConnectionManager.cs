// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using Microsoft.Data.SqlClient;

using System.Data.Common;

namespace Ark.Tools.Sql.SqlServer;

public class SqlConnectionManager : IDbConnectionManager
{
    protected static void OnInfoMessage(object sender, SqlInfoMessageEventArgs ev)
    {
        SqlExceptionHandler.LogSqlInfoMessage(ev);
    }

    public DbConnection Get(
#if NET10_0_OR_GREATER
    [InfrastructureSecret]
#endif
 string connectionString)
    {
        var conn = Build(connectionString);
        try
        {
            conn.Open();
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    public async Task<DbConnection> GetAsync(
#if NET10_0_OR_GREATER
    [InfrastructureSecret]
#endif
 string connectionString, CancellationToken ctk = default)
    {
        var conn = Build(connectionString);
        try
        {
            await conn.OpenAsync(ctk).ConfigureAwait(false);
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    protected virtual SqlConnection Build(
#if NET10_0_OR_GREATER
    [InfrastructureSecret]
#endif
 string connectionString)
    {
        var conn = new SqlConnection(connectionString);
        conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
        conn.FireInfoMessageEventOnUserErrors = false;
        return conn;
    }
}