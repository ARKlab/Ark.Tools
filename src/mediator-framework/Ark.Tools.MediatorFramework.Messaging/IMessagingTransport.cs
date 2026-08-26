// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Transport seam used by the messaging runtime without provider-specific types.</summary>
public interface IMessagingTransport
{
    /// <summary>Gets the capabilities declared by this transport.</summary>
    MessagingCapabilities Capabilities { get; }

    /// <summary>Gets the hard inline-envelope ceiling in bytes, or <see langword="null"/> when unbounded.</summary>
    long? MaximumInlineEnvelopeBytes { get; }

    /// <summary>Measures the completed native representation of an envelope.</summary>
    /// <param name="headers">The envelope headers.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <returns>The native representation size in bytes.</returns>
    long MeasureNative(IReadOnlyDictionary<string, string> headers, in ReadOnlySequence<byte> payload);

    /// <summary>Sends an envelope to a queue.</summary>
    /// <param name="queue">The destination queue.</param>
    /// <param name="headers">The envelope headers.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="dueTime">The optional scheduled delivery time.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes when the envelope is accepted.</returns>
    Task SendAsync(
        string queue,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        DateTimeOffset? dueTime,
        CancellationToken ctk);

    /// <summary>Publishes an envelope to a topic.</summary>
    /// <param name="topic">The destination topic.</param>
    /// <param name="headers">The envelope headers.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes when the envelope is accepted.</returns>
    Task PublishAsync(
        string topic,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        CancellationToken ctk);
}

/// <summary>Receive seam for transports that provide locked deliveries.</summary>
public interface IMessagingReceiveTransport : IMessagingTransport
{
    /// <summary>Streams locked deliveries from a queue until cancellation.</summary>
    /// <param name="queue">The source queue.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>An asynchronous delivery stream.</returns>
    IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(string queue, CancellationToken ctk);
}

/// <summary>A PeekLock-style delivery with exactly one settlement operation.</summary>
public interface IMessagingLockedDelivery
{
    /// <summary>Gets the received headers.</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the received serialized payload.</summary>
    ReadOnlySequence<byte> Payload { get; }

    /// <summary>Gets the native delivery count, starting at one.</summary>
    int DeliveryCount { get; }

    /// <summary>Renews the native lock for this delivery.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after the lock is renewed.</returns>
    Task RenewLockAsync(CancellationToken ctk);

    /// <summary>Completes and removes the delivery.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after settlement.</returns>
    Task CompleteAsync(CancellationToken ctk);

    /// <summary>Abandons the delivery and makes it visible again.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after settlement.</returns>
    Task AbandonAsync(CancellationToken ctk);

    /// <summary>Moves the delivery to the dead-letter store.</summary>
    /// <param name="reason">The bounded dead-letter reason.</param>
    /// <param name="description">The bounded dead-letter description.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after settlement.</returns>
    Task DeadLetterAsync(string reason, string description, CancellationToken ctk);
}

/// <summary>Optional resource-management operations for a messaging transport.</summary>
public interface IMessagingTransportManagement
{
    /// <summary>Ensures that a queue exists.</summary>
    Task EnsureQueueAsync(string queue, CancellationToken ctk);

    /// <summary>Ensures that a topic exists.</summary>
    Task EnsureTopicAsync(string topic, CancellationToken ctk);

    /// <summary>Ensures a topic subscription forwarding to a queue.</summary>
    Task EnsureSubscriptionAsync(string topic, string subscription, string forwardToQueue, CancellationToken ctk);

    /// <summary>Deletes a topic subscription.</summary>
    Task DeleteSubscriptionAsync(string topic, string subscription, CancellationToken ctk);
}
