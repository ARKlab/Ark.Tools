// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Core.Application.Messages;

/// <summary>Declares the API participant: sends background work, publishes nothing.</summary>
[MessagingParticipant(
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class SampleMessagingApiParticipant;

/// <summary>Declares the print worker: processes background work and publishes completed prints.</summary>
[MessagingParticipant(
    Identity = "ark-mediator-sample",
    Processes = new[] { typeof(ProcessBookPrintProcessRequest), typeof(CreateBookReviewRequest.V1) },
    Publishes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(SampleMessagingRetryPolicy))]
public sealed partial class SampleMessagingParticipant;

/// <summary>Declares the sample notification subscriber participant.</summary>
[MessagingParticipant(
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(SampleMessagingRetryPolicy))]
public sealed partial class SampleMessagingNotificationParticipant;

/// <summary>Declares the sample audit subscriber participant.</summary>
[MessagingParticipant(
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(SampleMessagingRetryPolicy))]
public sealed partial class SampleMessagingAuditParticipant;

/// <summary>Defines retry behavior for the sample messaging participant.</summary>
public sealed class SampleMessagingRetryPolicy : IMessagingRetryPolicy
{
    /// <inheritdoc />
    public int MaximumDeliveryCount => 2;

    /// <inheritdoc />
    public bool SecondLevelRetriesEnabled => true;

    /// <inheritdoc />
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);
}

/// <summary>Declares the sample messaging network.</summary>
[MessagingNetwork(
    Members = new[]
    {
        typeof(SampleMessagingApiParticipant),
        typeof(SampleMessagingParticipant),
        typeof(SampleMessagingNotificationParticipant),
        typeof(SampleMessagingAuditParticipant),
    },
    Requires = MessagingCapabilities.SendReceive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend,
    MaximumSchedulingDelaySeconds = 3600)]
public sealed partial class SampleMessagingNetwork;
