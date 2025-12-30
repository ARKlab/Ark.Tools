// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ResourceWatcher.Testing;
using Ark.Tools.ResourceWatcher.WorkerHost;

using Microsoft.Extensions.Configuration;

using NodaTime;

using Reqnroll;

using System;
using System.Globalization;
using System.IO;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]

namespace Ark.Tools.ResourceWatcher.Tests.Init
{
    /// <summary>
    /// Test host infrastructure for ResourceWatcher tests.
    /// Provides access to state provider, diagnostic listener, and worker host configuration.
    /// </summary>
    [Binding]
    public sealed class TestHost : IDisposable
    {
        private static IConfiguration? _configuration;

        public static IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("Configuration not initialized");

        public static TestableStateProvider StateProvider { get; } = new();
        public static TestingDiagnosticListener DiagnosticListener { get; } = new();
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

        [BeforeScenario(Order = 0)]
        public void BeforeScenario()
        {
            // Clear state and diagnostics before each scenario
            StateProvider.ClearAll();
            DiagnosticListener.Clear();
        }

        public void Dispose()
        {
            DiagnosticListener.Dispose();
        }
    }

    /// <summary>
    /// Test worker host configuration.
    /// </summary>
    public class TestHostConfig : DefaultHostConfig
    {
        public string? DbConnectionString { get; set; }
    }
}
