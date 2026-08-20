// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Declares the sample background message participant.</summary>
[MessagingParticipant(
    Processes = new[] { typeof(ProcessBookPrintProcessRequest) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class SampleMessagingParticipant;

/// <summary>Declares the sample messaging network.</summary>
[MessagingNetwork(
    Members = new[] { typeof(SampleMessagingParticipant) },
    Requires = MessagingCapabilities.Receive)]
public sealed class SampleMessagingNetwork;
