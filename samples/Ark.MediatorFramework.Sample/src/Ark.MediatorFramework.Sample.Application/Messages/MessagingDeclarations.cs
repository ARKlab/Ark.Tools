// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Declares the sample background message participant.</summary>
[MessagingParticipant(
    Processes = new[] { typeof(ProcessBookPrintProcessRequest) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(SampleMessagingRetryPolicy))]
public sealed partial class SampleMessagingParticipant;

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
    Members = new[] { typeof(SampleMessagingParticipant) },
    Requires = MessagingCapabilities.Receive | MessagingCapabilities.ScheduledSend,
    MaximumSchedulingDelaySeconds = 3600)]
public static partial class SampleMessagingNetwork;
