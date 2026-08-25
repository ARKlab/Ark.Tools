// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

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
public static partial class SampleMessagingNetwork
{
    /// <summary>Creates the in-memory network options used by the sample bus.</summary>
    /// <returns>The sample messaging network options.</returns>
    public static MessagingNetworkOptions CreateOptions()
    {
        return new MessagingNetworkOptions(
            typeof(SampleMessagingNetwork),
            new MessagingNetworkAttribute
            {
                Members = [typeof(SampleMessagingParticipant)],
                Requires = MessagingCapabilities.Receive,
                MaximumSchedulingDelay = TimeSpan.FromHours(1)
            });
    }
}
