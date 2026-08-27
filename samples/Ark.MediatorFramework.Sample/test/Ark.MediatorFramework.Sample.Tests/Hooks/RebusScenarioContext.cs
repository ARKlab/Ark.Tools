// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;

using Reqnroll;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Owns the isolated receiver used by one Reqnroll background-workflow scenario.</summary>
[Binding]
public sealed class RebusScenarioContext : IAsyncDisposable
{
    // Rebus can briefly report no work between outbox dequeue and message dispatch.
    private const int _requiredConsecutiveIdleSamples = 5;
    private static readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(5);
    private readonly SampleTestContext _sampleContext;
    private Container? _receiver;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="RebusScenarioContext"/> class.</summary>
    /// <param name="sampleContext">The scenario-owned application sender.</param>
    public RebusScenarioContext(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Starts an isolated receiver after the scenario sender is initialized.</summary>
    [BeforeScenario(Order = HooksOrder.RebusReceiver)]
    public void StartReceiver()
    {
        var application = _sampleContext.Application;
        _receiver = RebusProcessorComposition.BuildContainer(
            application.Network,
            useSqlStore: application.UsesSqlStore,
            connectionString: application.ConnectionString,
            clock: application.Clock,
            dataContextFactory: application.UsesSqlStore ? null : application.DataContextFactory,
            printCompletedNotificationService: application.PrintCompletedNotificationService,
            registerHandlers: container =>
            {
                SampleRebusEndpoints.RegisterHandlers(container);
            },
            configureOptions: options => options.AddInProcessMessageInspector(),
            configureTimeouts: timeouts => timeouts.StoreInMemoryTests());
        _receiver.Verify();
        _receiver.StartBus();
        application.StartOutboundBus();
    }

    /// <summary>Sends a message that demonstrates retry exhaustion.</summary>
    /// <param name="reason">The error reason carried by the message.</param>
    public async Task SendFailingMessageAsync(string reason)
    {
        await _sampleContext.Application.SendAsync(new FailingRebusRequest
        {
            Reason = reason,
        }).ConfigureAwait(false);
    }

    /// <summary>Waits for all background and outbox work to complete.</summary>
    [When("I wait for the background bus to be idle and the outbox to be empty")]
    public async Task WaitForBackgroundBus()
    {
        await WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Waits for all non-scheduled background and outbox work to complete.</summary>
    [When("I wait for the background bus to be idle and the outbox to be empty ignoring scheduled messages")]
    public async Task WaitForBackgroundBusIgnoringScheduledMessages()
    {
        await WaitForIdleAsync(ignoreDeferred: true).ConfigureAwait(false);
    }

    /// <summary>Waits until no scenario-owned background work remains.</summary>
    /// <param name="ignoreDeferred">Whether scheduled messages may remain.</param>
    /// <param name="allowErrors">Whether messages in the error queue are expected.</param>
    public async Task WaitForIdleAsync(bool ignoreDeferred = false, bool allowErrors = false)
    {
        using var cancellation = new CancellationTokenSource(_idleTimeout);
        var idleSamples = 0;
        try
        {
            while (true)
            {
                var counts = await _getWorkCountsAsync(cancellation.Token).ConfigureAwait(false);
                var pending = counts.InQueue + counts.InProcess + (ignoreDeferred ? 0 : counts.Deferred) + counts.Outbox;
                if (pending == 0 && (allowErrors || counts.Error == 0))
                {
                    if (++idleSamples >= _requiredConsecutiveIdleSamples)
                        return;
                }
                else
                {
                    idleSamples = 0;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception)
        {
            var counts = await _getWorkCountsAsync(CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException(
                $"Rebus did not become idle. queue={counts.InQueue}, in-process={counts.InProcess}, deferred={counts.Deferred}, outbox={counts.Outbox}, error={counts.Error}.",
                exception);
        }
    }

    /// <summary>Gets the number of failed messages currently in the error queue.</summary>
    public int ErrorQueueCount => _sampleContext.Application.Network.GetCount("error");

    /// <summary>Cleans up background resources before the scenario application container is disposed.</summary>
    [AfterScenario(Order = HooksOrder.RebusCleanup)]
    public async Task CleanupAsync()
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        using var drainer = DrainableInMemTransport.Drain();
        using var cleanupCancellation = new CancellationTokenSource(_idleTimeout);
        try
        {
            if (_receiver is not null)
            {
                await _receiver.DisposeAsync().ConfigureAwait(false);
                _receiver = null;
            }

            do
            {
                await _sampleContext.Application.ClearOutboxAsync(cleanupCancellation.Token).ConfigureAwait(false);
                TestsInMemoryTimeoutManager.ClearPendingDue();
                _sampleContext.Application.Network.Reset();
                await _waitForInProcessMessagesAsync(cleanupCancellation.Token).ConfigureAwait(false);
            }
            while (drainer.StillDraining);

            var remaining = await _getWorkCountsAsync(CancellationToken.None).ConfigureAwait(false);
            if (remaining != RebusWorkCounts.Empty)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Rebus cleanup left work behind. queue={0}, in-process={1}, deferred={2}, outbox={3}, error={4}.",
                        remaining.InQueue,
                        remaining.InProcess,
                        remaining.Deferred,
                        remaining.Outbox,
                        remaining.Error));
            }
        }
        catch (OperationCanceledException exception) when (cleanupCancellation.IsCancellationRequested)
        {
            var counts = await _getWorkCountsAsync(CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Rebus cleanup timed out. queue={0}, in-process={1}, deferred={2}, outbox={3}, error={4}.",
                    counts.InQueue,
                    counts.InProcess,
                    counts.Deferred,
                    counts.Outbox,
                    counts.Error),
                exception);
        }
    }

    private async Task<RebusWorkCounts> _getWorkCountsAsync(CancellationToken ctk)
    {
        var network = _sampleContext.Application.Network;
        var queues = network.Queues.ToArray();
        var inQueue = queues
            .Where(queue => !string.Equals(queue, "error", StringComparison.OrdinalIgnoreCase))
            .Sum(network.GetCount);
        var errors = queues
            .Where(queue => string.Equals(queue, "error", StringComparison.OrdinalIgnoreCase))
            .Sum(network.GetCount);
        var outbox = await _sampleContext.Application.GetOutboxCountAsync(ctk).ConfigureAwait(false);
        return new RebusWorkCounts(
            inQueue,
            InProcessMessageInspectorStep.Count,
            TestsInMemoryTimeoutManager.DueCount,
            outbox,
            errors);
    }

    private static async Task _waitForInProcessMessagesAsync(CancellationToken ctk)
    {
        while (InProcessMessageInspectorStep.Count > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(50), ctk).ConfigureAwait(false);
    }

    private sealed record RebusWorkCounts(int InQueue, int InProcess, int Deferred, int Outbox, int Error)
    {
        public static readonly RebusWorkCounts Empty = new(0, 0, 0, 0, 0);
    }

}
