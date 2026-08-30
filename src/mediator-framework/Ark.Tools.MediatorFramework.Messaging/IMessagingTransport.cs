// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Transport seam used by the messaging runtime without provider-specific types.</summary>
public interface IMessagingTransport
{
    /// <summary>Gets the capabilities declared by this transport.</summary>
    MessagingCapabilities Capabilities { get; }

    /// <summary>Gets the hard maximum complete payload size in bytes.</summary>
    long MaximumPayloadBytes { get; }

    /// <summary>Measures the native header representation of an envelope.</summary>
    /// <param name="headers">The envelope headers.</param>
    /// <returns>The native header representation size in bytes.</returns>
    long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers);

    /// <summary>Measures the complete native envelope for a serialized payload.</summary>
    /// <param name="headers">The envelope headers.</param>
    /// <param name="payload">The serialized payload.</param>
    /// <returns>The native envelope size in bytes.</returns>
    long MeasureNativePayload(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload)
    {
        return checked(MeasureNativeHeaders(headers) + payload.Length);
    }

    /// <summary>Static transport contract for provider-native sizing and naming.</summary>
    /// <typeparam name="TSelf">The implementing transport class itself.</typeparam>
    public interface IMessagingTransport<TSelf>
        where TSelf : IMessagingTransport<TSelf>
    {
        /// <summary>Gets the fixed native payload limit for the transport.</summary>
        static abstract long MaximumPayloadLimitBytes { get; }

        /// <summary>Measures the native header representation.</summary>
        /// <param name="headers">The envelope headers.</param>
        /// <returns>The native header size in bytes.</returns>
        static abstract long GetNativeHeaderSize(IReadOnlyDictionary<string, string> headers);

        /// <summary>Maps a logical name to a provider-native entity name.</summary>
        /// <param name="logicalName">The logical entity name.</param>
        /// <returns>The provider-native entity name.</returns>
        static abstract string ToNativeEntityName(string logicalName);
    }

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

    /// <summary>Renews the native lock for this delivery when supported.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after the lock is renewed.</returns>
    Task RenewLockAsync(CancellationToken ctk) => Task.CompletedTask;

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
    /// <param name="queue">The queue name.</param>
    /// <param name="maximumDeliveryCount">The native maximum delivery count.</param>
    /// <param name="ownerIdentity">The participant that owns the queue.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after the queue is ensured.</returns>
    Task EnsureQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk);

    /// <summary>Ensures that a topic exists.</summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="ownerIdentity">The publishing participant identity.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after the topic is ensured.</returns>
    Task EnsureTopicAsync(string topic, string ownerIdentity, CancellationToken ctk);

    /// <summary>Ensures a topic subscription forwarding to a queue.</summary>
    /// <param name="subscription">The desired subscription.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after the subscription is ensured.</returns>
    Task EnsureSubscriptionAsync(
        MessagingSubscriptionResource subscription,
        CancellationToken ctk);

    /// <summary>Gets the existing subscriptions for a topic.</summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The existing subscription descriptors.</returns>
    Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
        string topic,
        CancellationToken ctk);

    /// <summary>Deletes a topic subscription.</summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="subscription">The subscription name.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after deletion.</returns>
    Task DeleteSubscriptionAsync(string topic, string subscription, CancellationToken ctk);
}
