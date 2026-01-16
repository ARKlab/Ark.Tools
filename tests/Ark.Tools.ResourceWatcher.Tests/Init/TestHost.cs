// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
#pragma warning disable IDE0005 // Using directives needed for nested types 
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;
using Ark.Tools.Sql.SqlServer;

using Microsoft.Extensions.Configuration;

using NodaTime;

using Reqnroll;
#pragma warning restore IDE0005

// Scenarios can run in parallel, but SQL integration tests must run sequentially
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]

namespace Ark.Tools.ResourceWatcher.Tests.Init;

/// <summary>
/// Test host infrastructure for ResourceWatcher tests.
/// Provides shared configuration across test scenarios.
/// Each scenario creates its own WorkerHost instance with dedicated state provider and diagnostic listener.
/// </summary>
[Binding]
public sealed class TestHost
{
    private static IConfiguration? _configuration;
    private static bool _dbSchemaInitialized;

    public static IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("Configuration not initialized");

    public static TestHostConfig WorkerConfig { get; private set; } = new();

    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.IntegrationTests.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        WorkerConfig = new TestHostConfig
        {
            WorkerName = _configuration["Worker:Name"] ?? "TestWorker",
            MaxRetries = uint.Parse(_configuration["Worker:MaxRetries"] ?? "3", CultureInfo.InvariantCulture),
            Sleep = TimeSpan.Parse(_configuration["Worker:Sleep"] ?? "00:00:01", CultureInfo.InvariantCulture),
            DegreeOfParallelism = uint.Parse(_configuration["Worker:DegreeOfParallelism"] ?? "1", CultureInfo.InvariantCulture),
            BanDuration = Duration.FromHours(int.Parse(_configuration["Worker:BanDurationHours"] ?? "24", CultureInfo.InvariantCulture))
        };

        // Initialize database schema once for all tests
        InitializeDatabaseSchema();
    }

    private static void InitializeDatabaseSchema()
    {
        if (_dbSchemaInitialized)
            return;

        var connectionString = _configuration!["ConnectionStrings:SqlServer"];
        if (string.IsNullOrEmpty(connectionString))
            return; // Skip if no SQL Server configured

        try
        {
            lock (DbSetupLock.Instance)
            {
                // Use VoidExtensions provider to create schema (works for all extension types)
                var config = new TestSqlStateProviderConfig
                {
                    DbConnectionString = connectionString
                };
                var connManager = new SqlConnectionManager();
                var provider = new SqlStateProvider<VoidExtensions>(config, connManager);
                
                // Ensure database and tables exist
                provider.EnsureTableAreCreated();
                _dbSchemaInitialized = true;
            }
        }
#pragma warning disable ERP022 // Test infrastructure: Best-effort DB setup
        catch (Exception)
        {
            // Swallow - individual tests will handle initialization if this fails
            // This is a best-effort one-time setup
        }
#pragma warning restore ERP022
    }

    private sealed class TestSqlStateProviderConfig : ISqlStateProviderConfig
    {
        public required string DbConnectionString { get; init; }
        public System.Text.Json.Serialization.JsonSerializerContext? ExtensionsJsonContext => null;
    }
}

/// <summary>
/// Test worker host configuration.
/// </summary>
public class TestHostConfig : DefaultHostConfig
{
    public string? DbConnectionString { get; set; }
}