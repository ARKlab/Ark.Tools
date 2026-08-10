// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Outbox;
using Ark.MediatorFramework.Sample.RebusProcessor;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Rebus;

using AwesomeAssertions;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Demonstrates a serialized process-wide application fixture.</summary>
[TestClass]
[DoNotParallelize]
public sealed class ProcessWideApplicationFixtureTests
{
    /// <summary>Resets the shared fixture between two serialized scenarios.</summary>
    [TestMethod]
    [TestCategory("process-wide-fixture")]
    public async Task SharedFixtureResetsBetweenScenarios()
    {
        await using var fixture = new ProcessWideApplicationFixture();

        await fixture.RunScenarioAsync("first").ConfigureAwait(false);
        (await fixture.CountGreetingsAsync().ConfigureAwait(false)).Should().Be(1);
        await fixture.RunScenarioAsync("second").ConfigureAwait(false);
        (await fixture.CountGreetingsAsync().ConfigureAwait(false)).Should().Be(1);
    }
}

internal sealed class ProcessWideApplicationFixture : IAsyncDisposable
{
    private readonly ApplicationTestContext _context;
    private readonly Container _processor;
    private readonly SemaphoreSlim _serial = new(1, 1);

    internal ProcessWideApplicationFixture()
    {
        DataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        _context = new ApplicationTestContext(useSqlStore: false, dataContextFactory: DataContextFactory);
        _processor = RebusProcessorComposition.BuildContainer(
            _context.Network,
            useSqlStore: false,
            dataContextFactory: DataContextFactory,
            registerHandlers: SampleRebusEndpoints.RegisterHandlers);
        _processor.Verify();
        _processor.StartBus();
    }

    internal InMemorySampleDataContextFactory DataContextFactory { get; }

    internal async Task RunScenarioAsync(string name)
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try
        {
            DataContextFactory.Reset();
            await _context.DispatchRequestAsync<ComposeGreetingRequest, ComposeGreetingResponse>(
                new ComposeGreetingRequest { Name = name }).ConfigureAwait(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (await CountGreetingsAsync(timeout.Token).ConfigureAwait(false) == 0)
                await Task.Delay(50, timeout.Token).ConfigureAwait(false);
        }

        finally
        {
            _serial.Release();
        }
    }

    internal async Task<int> CountGreetingsAsync(CancellationToken ctk = default)
    {
        var page = await _context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(
            new SearchGreetingsQuery { Limit = 1 },
            ctk).ConfigureAwait(false);
        return checked((int)page.Count);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync().ConfigureAwait(false);
        await _context.DisposeAsync().ConfigureAwait(false);
        _serial.Dispose();
    }
}
