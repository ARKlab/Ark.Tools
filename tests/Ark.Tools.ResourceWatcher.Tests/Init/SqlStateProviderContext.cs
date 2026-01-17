// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Nodatime.Dapper;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using AwesomeAssertions;

using Dapper;

using Reqnroll;

namespace Ark.Tools.ResourceWatcher.Tests.Init;

/// <summary>
/// Shared context for SQL Server database and SqlStateProvider configuration.
/// Provides common database setup functionality for all ResourceWatcher tests.
/// This follows the Driver pattern - reusable state and verbs across test scenarios.
/// </summary>
[Binding]
public sealed class SqlStateProviderContext
{
    private ISqlStateProviderConfig? _config;
    private IDbConnectionManager? _connectionManager;
    private readonly string _testRunId = Guid.NewGuid().ToString("N")[..8];

    public ISqlStateProviderConfig Config
    {
        get => _config ?? throw new InvalidOperationException("Config not initialized. Call InitializeDatabase() first.");
        private set => _config = value;
    }

    public IDbConnectionManager ConnectionManager
    {
        get => _connectionManager ?? throw new InvalidOperationException("ConnectionManager not initialized. Call InitializeDatabase() first.");
        private set => _connectionManager = value;
    }

    /// <summary>
    /// Gets a unique tenant name for this test run to avoid parallel test conflicts.
    /// </summary>
    public string GetUniqueTenant(string baseTenant)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{baseTenant}-{_testRunId}");
    }

    /// <summary>
    /// Initializes the SQL Server database connection and ensures database exists.
    /// This is a reusable verb that can be called from any test scenario.
    /// </summary>
    public void InitializeDatabase()
    {
        // Setup NodaTime Dapper type handlers for DateTime <-> NodaTime conversions
        NodaTimeDapper.Setup();

        var connectionString = TestHost.Configuration["ConnectionStrings:SqlServer"];
        connectionString.Should().NotBeNullOrEmpty("SQL Server connection string must be configured in appsettings.IntegrationTests.json");

        Config = new SqlStateProviderConfigImpl
        {
            DbConnectionString = connectionString!
        };

        // Ensure database exists
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var masterConn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        masterConn.Open();
        masterConn.Execute(string.Create(CultureInfo.InvariantCulture, $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName}')
            BEGIN
                CREATE DATABASE [{dbName}]
            END"));

        ConnectionManager = new SqlConnectionManager();
    }

    /// <summary>
    /// Simple implementation of ISqlStateProviderConfig for testing.
    /// </summary>
    private sealed class SqlStateProviderConfigImpl : ISqlStateProviderConfig
    {
        public required string DbConnectionString { get; init; }
        public System.Text.Json.Serialization.JsonSerializerContext? ExtensionsJsonContext => null;
    }
}
