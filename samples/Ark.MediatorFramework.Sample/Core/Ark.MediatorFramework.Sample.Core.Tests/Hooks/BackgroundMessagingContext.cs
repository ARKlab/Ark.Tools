// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>Observes the scenario's background messaging: outbox, participant queues, and dead letters.</summary>
[Binding]
public sealed class BackgroundMessagingContext
{
    // A delivery is briefly invisible between the outbox dequeue and the transport send.
    private const int _requiredConsecutiveIdleSamples = 5;
    private static readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(5);
    private readonly SampleTestContext _sampleContext;

    /// <summary>Initializes a new instance of the <see cref="BackgroundMessagingContext"/> class.</summary>
    /// <param name="sampleContext">The scenario-owned application.</param>
    public BackgroundMessagingContext(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Gets the number of dead-lettered messages across the participant queues.</summary>
    public int ErrorQueueCount => DeadLetters.Count;

    /// <summary>Gets the dead-lettered messages across the participant queues.</summary>
    public IReadOnlyList<InMemoryDeadLetter> DeadLetters => InMemoryMessagingHarness.Queues
        .SelectMany(_sampleContext.Application.Transport.GetDeadLetters)
        .ToArray();

    /// <summary>Waits for all background and outbox work to complete.</summary>
    [When("I wait for the background bus to be idle and the outbox to be empty")]
    public async Task WaitForBackgroundBus()
    {
        await WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Waits for all background and outbox work to complete.</summary>
    /// <remarks>Kept for feature compatibility: the in-memory transport has no separately scheduled work here.</remarks>
    [When("I wait for the background bus to be idle and the outbox to be empty ignoring scheduled messages")]
    public async Task WaitForBackgroundBusIgnoringScheduledMessages()
    {
        await WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Waits until no scenario-owned background work remains.</summary>
    /// <param name="allowErrors">Whether dead-lettered messages are expected.</param>
    public async Task WaitForIdleAsync(bool allowErrors = false)
    {
        using var cancellation = new CancellationTokenSource(_idleTimeout);
        var idleSamples = 0;
        try
        {
            while (true)
            {
                var (pending, deadLetters) = await _getWorkAsync(cancellation.Token).ConfigureAwait(false);
                if (pending == 0 && (allowErrors || deadLetters == 0))
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
            var (pending, deadLetters) = await _getWorkAsync(CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Background messaging did not become idle. pending={0}, dead-letters={1}.",
                    pending,
                    deadLetters),
                exception);
        }
    }

    private async Task<(int Pending, int DeadLetters)> _getWorkAsync(CancellationToken ctk)
    {
        var application = _sampleContext.Application;
        var pending = await application.GetOutboxCountAsync(ctk).ConfigureAwait(false);
        var deadLetters = 0;
        foreach (var queue in InMemoryMessagingHarness.Queues)
        {
            pending += application.Transport.GetPendingCount(queue);
            deadLetters += application.Transport.GetDeadLetters(queue).Count;
        }
        return (pending, deadLetters);
    }
}
