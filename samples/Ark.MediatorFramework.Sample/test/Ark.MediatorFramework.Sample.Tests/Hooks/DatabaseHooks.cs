// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Creates and resets the sample SQL database for integration runs.</summary>
[Binding]
public sealed class DatabaseHooks
{
    /// <summary>Gets the SQL connection string used by the sample integration database.</summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("ARK_SAMPLE_SQL_CONNECTION")
        ?? new SqlConnectionStringBuilder
        {
            DataSource = "localhost,1433",
            InitialCatalog = "Ark.MediatorFramework.Sample",
            UserID = "sa",
            Password = string.Concat("Integration", "Tests", "Db", "Password", 85, '!'),
            TrustServerCertificate = true,
            Encrypt = false,
        }.ConnectionString;

    /// <summary>Creates the sample schema when SQL integration tests are enabled.</summary>
    [BeforeTestRun(Order = HooksOrder.DatabaseSetup)]
    public static void EnsureDatabase()
    {
        if (!_sqlEnabled())
            return;

        var builder = new SqlConnectionStringBuilder(ConnectionString);
        builder.Remove("Initial Catalog");
        var dacpacPath = Path.Combine(
            AppContext.BaseDirectory,
            "Ark.MediatorFramework.Sample.Database.dacpac");
        using var dacpac = DacPackage.Load(dacpacPath);
        var instance = new DacServices(builder.ConnectionString);
        instance.Deploy(
            dacpac,
            "Ark.MediatorFramework.Sample",
            upgradeExisting: true,
            new DacDeployOptions
            {
                CreateNewDatabase = true,
                AllowIncompatiblePlatform = true,
            });
    }

    /// <summary>Clears SQL state between scenarios when SQL integration tests are enabled.</summary>
    [BeforeScenario(Order = HooksOrder.DatabaseReset)]
    public static async Task ResetDatabaseAsync()
    {
        if (!_sqlEnabled())
            return;

        var connection = new SqlConnection(ConnectionString);
        await using var __ctx = connection.ConfigureAwait(false);
        await connection.OpenAsync().ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var __command = command.ConfigureAwait(false);
        command.CommandText = "[ops].[ResetFull_OnlyForTesting]";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@areYouReallySure", System.Data.SqlDbType.Bit);
        parameter.Value = true;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static bool _sqlEnabled()
    {
        return !string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_INMEMORY_TESTS"),
            "1",
            StringComparison.Ordinal);
    }
}
