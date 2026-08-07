// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Tests.Auth;
using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Rebus.Tests;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

using Flurl.Http;

using Microsoft.SqlServer.Dac;

using System.Diagnostics;

namespace Ark.Reference.Profiling;

internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(ReferenceEndpointBenchmarks).Assembly)
            .Run(args);
    }
}

/// <summary>
/// Profiles the Ark.Reference HTTP endpoints through the integration-test host.
/// </summary>
[Config(typeof(ReferenceBenchmarkConfig))]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ReferenceEndpointBenchmarks
{
    private const int RequestsPerBenchmark = 10;
    private static readonly TimeSpan RebusIdleTimeout = TimeSpan.FromMinutes(15);

    private readonly AuthTestContext _auth = new();
    private IFlurlClient? _client;
    private Book.V1.Output[] _books = [];
    private int _bookSequence;

    /// <summary>
    /// Deploys the database, starts the host, and creates benchmark seed data.
    /// </summary>
    [GlobalSetup]
    public async Task Setup()
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        DeployDatabase();
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Core.Database",
            $"{DatabaseUtils.DatabaseConnectionString};Initial Catalog=Ark.Reference.Core.Database");
        TestHost.BeforeTests0();
        TestHost.BeforeTests();

        _client = TestHost.Factory.Get(new Uri("https://localhost:5001"));
        _books = new Book.V1.Output[RequestsPerBenchmark];
        for (var index = 0; index < _books.Length; index++)
            _books[index] = await CreateBook().ConfigureAwait(false);
    }

    /// <summary>
    /// Configures a short profiling run with one ten-request benchmark invocation per iteration.
    /// </summary>
    public sealed class ReferenceBenchmarkConfig : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceBenchmarkConfig"/> class.
        /// </summary>
        public ReferenceBenchmarkConfig()
        {
            AddJob(Job.ShortRun
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
        }
    }

    /// <summary>
    /// Executes <c>POST /v1/book</c> ten times.
    /// </summary>
    [Benchmark]
    public async Task PostBook()
    {
        for (var index = 0; index < RequestsPerBenchmark; index++)
            _ = await CreateBook().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes <c>GET /v1/book/{id}</c> ten times.
    /// </summary>
    [Benchmark]
    public async Task GetBook()
    {
        var client = GetClient();
        for (var index = 0; index < RequestsPerBenchmark; index++)
        {
            _ = await Send(client, $"v1/book/{_books[index].Id}")
                .GetJsonAsync<Book.V1.Output>()
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes <c>POST /v1/ping/message</c> ten times.
    /// </summary>
    [Benchmark]
    public async Task PostPingMessage()
    {
        var client = GetClient();
        for (var index = 0; index < RequestsPerBenchmark; index++)
        {
            _ = await Send(client, "v1/ping/message")
                .PostJsonAsync(new Ping.V1.Create
                {
                    Name = $"Profiling ping {index}",
                    Type = PingType.Ping1
                })
                .ReceiveJson<Ping.V1.Output>()
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes <c>POST /v1/bookPrintProcess</c> ten times.
    /// </summary>
    [Benchmark]
    public async Task PostBookPrintProcess()
    {
        var client = GetClient();
        for (var index = 0; index < RequestsPerBenchmark; index++)
        {
            _ = await Send(client, "v1/bookPrintProcess")
                .PostJsonAsync(new BookPrintProcess.V1.Create
                {
                    BookId = _books[index].Id,
                    ShouldFail = false
                })
                .ReceiveJson<BookPrintProcess.V1.Output>()
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits for Rebus to become idle after each benchmark iteration.
    /// </summary>
    [IterationCleanup]
    public async Task WaitForRebusToBecomeIdle()
    {
        var timeout = Stopwatch.StartNew();
        while (TestHost.Env.RebusNetwork.Count() != 0 || InProcessMessageInspectorStep.Count != 0)
        {
            if (timeout.Elapsed >= RebusIdleTimeout)
                throw new TimeoutException("Timed out waiting for Rebus to become idle.");

            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops and disposes the integration-test host.
    /// </summary>
    [GlobalCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        await TestHost.Server.StopAsync().ConfigureAwait(false);
        TestHost.AfterTests();
    }

    private async Task<Book.V1.Output> CreateBook()
    {
        var sequence = _bookSequence++;
        return await Send(GetClient(), "v1/book")
            .PostJsonAsync(new Book.V1.Create
            {
                Title = $"Profiling book {sequence}",
                Author = "Ark.Tools",
                Genre = BookGenre.Technology,
                ISBN = $"978-0135957{sequence % 10000:D4}"
            })
            .ReceiveJson<Book.V1.Output>()
            .ConfigureAwait(false);
    }

    private IFlurlClient GetClient()
    {
        return _client ?? throw new InvalidOperationException("The benchmark host has not been started.");
    }

    private IFlurlRequest Send(IFlurlClient client, string path)
    {
        return _auth.SetAuth(client.Request(path));
    }

    private static void DeployDatabase()
    {
        var dacpacPath = Path.Combine(AppContext.BaseDirectory, "Ark.Reference.Core.Database.dacpac");
        using var dacpac = DacPackage.Load(dacpacPath);
        var services = new DacServices(DatabaseUtils.DatabaseConnectionString);
        services.Deploy(
            dacpac,
            "Ark.Reference.Core.Database",
            upgradeExisting: true,
            new DacDeployOptions
            {
                CreateNewDatabase = true,
                AllowIncompatiblePlatform = true
            });
    }
}
