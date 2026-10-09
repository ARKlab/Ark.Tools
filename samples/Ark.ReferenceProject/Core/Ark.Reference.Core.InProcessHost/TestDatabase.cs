// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using Microsoft.Data.SqlClient;

namespace Ark.Reference.Core.InProcessHost;

/// <summary>
/// Local SQL Server used by the test host.
/// </summary>
public static class TestDatabase
{
    /// <summary>
    /// Gets the connection string of the local test SQL Server.
    /// </summary>
    [InfrastructureSecret]
    public const string ConnectionString = @"Data Source=127.0.0.1;User Id=sa;Password=IntegrationTestsDbPassword85!;Pooling=True;Connect Timeout=60;Encrypt=True;TrustServerCertificate=True";

    /// <summary>
    /// Creates the logging database when it does not already exist.
    /// </summary>
    public static async Task CreateNLogDatabaseIfNotExists()
    {
        var conn = new SqlConnection(ConnectionString);
        await using var _ = conn.ConfigureAwait(false);
        await conn.OpenAsync().ConfigureAwait(false);
        using var cmd = new SqlCommand("IF (db_id(N'Logs') IS NULL) BEGIN CREATE DATABASE [Logs] END;", conn);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
