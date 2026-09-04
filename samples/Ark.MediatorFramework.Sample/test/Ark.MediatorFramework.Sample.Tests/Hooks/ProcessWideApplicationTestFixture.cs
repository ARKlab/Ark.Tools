// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Outbox;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;

using Rebus.Transport.InMem;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>
/// Owns a process-wide application and processor for tests that explicitly demonstrate
/// serialized fixture usage.
/// </summary>
public sealed class ProcessWideApplicationTestFixture : IAsyncDisposable
{
    private readonly SemaphoreSlim _scenarioGate = new(1, 1);
    private readonly InMemorySampleDataContextFactory _store;
    private readonly Container _processor;
    private int _activeScenarios;
    private int _maximumConcurrentScenarios;
    private bool _disposed;

    /// <summary>Initializes the shared application, store, network, and processor.</summary>
    public ProcessWideApplicationTestFixture()
    {
        Network = new InMemNetwork();
        _store = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        Application = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: _store,
            network: Network);
        _processor = RebusProcessorComposition.BuildContainer(
            Network,
            useSqlStore: false,
            clock: Application.Clock,
            dataContextFactory: _store,
            printCompletedNotificationService: Application.PrintCompletedNotificationService,
            configureOptions: options => options.AddInProcessMessageInspector(),
            configureTimeouts: timeouts => timeouts.StoreInMemoryTests());
        _processor.Verify();
        _processor.StartBus();
        Application.StartOutboundBus();
    }

    /// <summary>Gets the shared direct application container facade.</summary>
    public ApplicationTestContext Application { get; }

    /// <summary>Gets the shared in-memory transport network.</summary>
    public InMemNetwork Network { get; }

    /// <summary>Gets the shared processor container.</summary>
    public Container Processor => _processor;

    /// <summary>Gets the maximum number of scenarios observed inside the fixture.</summary>
    public int MaximumConcurrentScenarios => Volatile.Read(ref _maximumConcurrentScenarios);

    /// <summary>Gets whether the shared resources have been disposed.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Runs one serialized scenario and resets shared persistence and transport state around it.
    /// </summary>
    /// <typeparam name="T">The scenario result type.</typeparam>
    /// <param name="scenario">The scenario body.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The scenario result.</returns>
    public async Task<T> RunScenarioAsync<T>(
        Func<ApplicationTestContext, Task<T>> scenario,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _scenarioGate.WaitAsync(ctk).ConfigureAwait(false);
        var active = Interlocked.Increment(ref _activeScenarios);
        _setMaximumConcurrentScenarios(active);
        try
        {
            await _resetAsync(ctk).ConfigureAwait(false);
            return await scenario(Application).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _resetAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeScenarios);
                _scenarioGate.Release();
            }
        }
    }

    /// <summary>Disposes the processor before disposing the shared application resources.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _processor.DisposeAsync().ConfigureAwait(false);
        await Application.DisposeAsync().ConfigureAwait(false);
        _scenarioGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task _resetAsync(CancellationToken ctk)
    {
        _store.Reset();
        await Application.ClearOutboxAsync(ctk).ConfigureAwait(false);
        Network.Reset();
    }

    private void _setMaximumConcurrentScenarios(int active)
    {
        while (true)
        {
            var maximum = Volatile.Read(ref _maximumConcurrentScenarios);
            if (active <= maximum
                || Interlocked.CompareExchange(ref _maximumConcurrentScenarios, active, maximum) == maximum)
                return;
        }
    }
}
