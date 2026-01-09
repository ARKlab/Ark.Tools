// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ResourceWatcher.WorkerHost;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NodaTime;

using Reqnroll;

using System;
using System.Globalization;
using System.IO;

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
    }
}

/// <summary>
/// Test worker host configuration.
/// </summary>
public class TestHostConfig : DefaultHostConfig
{
    public string? DbConnectionString { get; set; }
}
