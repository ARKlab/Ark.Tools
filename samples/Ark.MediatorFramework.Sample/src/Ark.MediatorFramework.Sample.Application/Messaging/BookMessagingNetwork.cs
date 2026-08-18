// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Application.Messaging;

/// <summary>Shared messaging network used by the Book background activities.</summary>
[MessagingNetwork(
    MessagingCapabilities.Receive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend,
    Contracts = new[]
    {
        typeof(ProcessBookPrintProcessRequest),
        typeof(FailingRebusRequest),
        typeof(BookPrintCompleted),
    },
    DefaultSerializer = SerializationProtocol.Json,
    Compression = CompressionAlgorithm.Brotli,
    CompressionMinimumSizeBytes = 4096,
    MaximumTransportPayloadBytes = 240_000,
    RetryPolicy = typeof(BookMessagingRetryPolicy))]
public sealed class BookMessagingNetwork
{
}

/// <summary>Retry policy for Book background activities.</summary>
public sealed class BookMessagingRetryPolicy : IMessagingRetryPolicy
{
    /// <inheritdoc />
    public int MaximumDeliveryCount => 5;

    /// <inheritdoc />
    public bool SecondLevelRetriesEnabled => true;

    /// <inheritdoc />
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(30);
}
