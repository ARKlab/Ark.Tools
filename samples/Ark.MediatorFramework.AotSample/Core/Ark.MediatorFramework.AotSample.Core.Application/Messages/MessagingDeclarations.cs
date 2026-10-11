// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.Tools.MediatorFramework;

namespace Ark.MediatorFramework.AotSample.Core.Application.Messages;

/// <summary>The WebInterface: it only sends, through the outbox.</summary>
[MessagingParticipant(
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class AotApiParticipant;

/// <summary>The Processor: it processes <see cref="PrintBookCommand"/>.</summary>
[MessagingParticipant(
    Identity = "ark-mediator-aot-processor",
    Processes = new[] { typeof(PrintBookCommand) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(AotRetryPolicy))]
public sealed partial class AotProcessorParticipant;

/// <summary>Retries a failed print twice, one second apart.</summary>
public sealed class AotRetryPolicy : IMessagingRetryPolicy
{
    /// <inheritdoc />
    public int MaximumDeliveryCount => 3;

    /// <inheritdoc />
    public bool SecondLevelRetriesEnabled => false;

    /// <inheritdoc />
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);
}

/// <summary>The messaging network of the sample.</summary>
[MessagingNetwork(
    Members = new[] { typeof(AotApiParticipant), typeof(AotProcessorParticipant) },
    Requires = MessagingCapabilities.SendReceive,
    MaximumSchedulingDelaySeconds = 3600)]
public sealed partial class AotMessagingNetwork;
