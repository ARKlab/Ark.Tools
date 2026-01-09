// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Oracle.ManagedDataAccess.Client;

using System.Data.Common;

namespace Ark.Tools.Sql.Oracle;

/// <summary>
/// Default connection manager for Oracle databases using Oracle.ManagedDataAccess.Client.
/// Sets CommandTimeout to 30 seconds by default to align with industry standards.
/// </summary>
/// <remarks>
/// See Oracle documentation:
/// - <a href="https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/ConnectionProperties.html">OracleConnection Properties Reference</a>
/// - <a href="https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/CommandProperties.html">OracleCommand Properties Reference</a>
/// </remarks>
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

    /// <summary>
    /// Builds and configures an OracleConnection instance.
    /// </summary>
    /// <param name="connectionString">The connection string for the Oracle database.</param>
    /// <returns>A configured OracleConnection with CommandTimeout set to 30 seconds.</returns>
    /// <remarks>
    /// The CommandTimeout property is set to 30 seconds to prevent unbounded query execution,
    /// aligning with .NET ADO.NET standards (e.g., SQL Server default is 30 seconds).
    /// Commands created from this connection will inherit the 30-second timeout unless explicitly overridden.
    /// See: <a href="https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/ConnectionProperties.html">Oracle Connection Properties Documentation</a>
    /// </remarks>
    protected virtual OracleConnection Build(string connectionString)
    {
        var conn = new OracleConnection(connectionString);

        conn.InfoMessage += new OracleInfoMessageEventHandler(OnInfoMessage);

        // Set CommandTimeout to 30 seconds to align with industry standard practice.
        // This prevents unbounded or runaway queries by default while still allowing
        // consumers to override per command or per connection.
        // See: https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/ConnectionProperties.html
        conn.CommandTimeout = 30;

        return conn;
    }
}