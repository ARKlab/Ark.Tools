// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Pull-style delivery source with explicit credits and an empty signal.</summary>
/// <remarks>
/// This is the processing seam and is composed only by processor hosts. The send seam
/// (<see cref="IMessagingTransport"/>) is unaffected, and hosts that own triggering
/// themselves (Azure Functions) never reach this seam.
/// </remarks>
public interface IMessagingMessageSource
{
    /// <summary>Gets the capabilities of this receiver, read once at composition time.</summary>
    MessagingReceiverCapabilities ReceiverCapabilities { get; }

    /// <summary>Receives at most <paramref name="maxMessages"/> locked deliveries.</summary>
    /// <param name="queue">The source queue.</param>
    /// <param name="maxMessages">The maximum number of deliveries to return; must be positive.</param>
    /// <param name="maxWait">The maximum time to wait for the first delivery; must not be negative.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>
    /// Zero or more locked deliveries, never more than <paramref name="maxMessages"/>. An empty
    /// result means "the queue is empty" and is returned after at most <paramref name="maxWait"/>;
    /// it is never an error and never means "the source ended". Deciding how long to wait before
    /// the next call belongs to the host, so the source never sleeps on an empty result.
    /// </returns>
    ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
        string queue,
        int maxMessages,
        TimeSpan maxWait,
        CancellationToken ctk);
}

/// <summary>The native receive capabilities declared by a message source.</summary>
/// <param name="MaximumBatchSize">The maximum number of deliveries a single receive can return.</param>
/// <param name="SupportsServerSideWait">
/// Whether the broker holds a receive open for the requested wait window instead of returning
/// immediately, so a host can grow the wait window rather than sleep between calls.
/// </param>
/// <param name="SupportsLockRenewal">Whether <see cref="IMessagingLockedDelivery.RenewLockAsync"/> extends the native lock.</param>
/// <param name="NativeLockDuration">The native lock duration, or <see langword="null"/> when unknown.</param>
public sealed record MessagingReceiverCapabilities(
    int MaximumBatchSize,
    bool SupportsServerSideWait,
    bool SupportsLockRenewal,
    TimeSpan? NativeLockDuration)
{
    /// <summary>Gets the maximum number of deliveries a single receive can return.</summary>
    public int MaximumBatchSize { get; } = MaximumBatchSize > 0
        ? MaximumBatchSize
        : throw new ArgumentOutOfRangeException(
            nameof(MaximumBatchSize),
            "The maximum batch size must be positive.");

    /// <summary>Gets the native lock duration, or <see langword="null"/> when unknown.</summary>
    public TimeSpan? NativeLockDuration { get; } = NativeLockDuration is null || NativeLockDuration > TimeSpan.Zero
        ? NativeLockDuration
        : throw new ArgumentOutOfRangeException(
            nameof(NativeLockDuration),
            "The native lock duration must be positive when specified.");
}
