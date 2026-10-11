// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;

namespace Ark.MediatorFramework.Sample.Aot.Application;

/// <summary>The Api app: it only sends.</summary>
[MessagingParticipant(
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class AotApiParticipant;

/// <summary>The Processor app: it processes <see cref="ProcessGreetingCommand"/>.</summary>
[MessagingParticipant(
    Identity = "ark-mediator-aot",
    Processes = new[] { typeof(ProcessGreetingCommand) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(AotRetryPolicy))]
public sealed partial class AotProcessorParticipant;

/// <summary>Retries a failed greeting twice, one second apart.</summary>
public sealed class AotRetryPolicy : IMessagingRetryPolicy
{
    /// <inheritdoc />
    public int MaximumDeliveryCount => 2;

    /// <inheritdoc />
    public bool SecondLevelRetriesEnabled => false;

    /// <inheritdoc />
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);
}

/// <summary>The messaging network of the AOT sample.</summary>
[MessagingNetwork(
    Members = new[] { typeof(AotApiParticipant), typeof(AotProcessorParticipant) },
    Requires = MessagingCapabilities.SendReceive,
    MaximumSchedulingDelaySeconds = 3600)]
public sealed partial class AotMessagingNetwork;
