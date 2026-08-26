// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

[assembly: Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHost(
    typeof(BoundaryMessagingParticipant),
    Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.ServiceBus,
    ConnectionConfigurationKey = "BoundaryMessaging")]

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

/// <summary>Compile fixture message for the generated Service Bus trigger.</summary>
[Message(Name = "boundary_process")]
public sealed class BoundaryMessage : ICommand<BoundaryMessage>
{
}

/// <summary>Compile fixture participant for the generated Service Bus trigger.</summary>
[MessagingParticipant(
    Processes = new[] { typeof(BoundaryMessage) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class BoundaryMessagingParticipant
{
}

/// <summary>Compile fixture network for the generated Service Bus trigger.</summary>
[MessagingNetwork(
    Members = new[] { typeof(BoundaryMessagingParticipant) },
    Requires = MessagingCapabilities.Receive)]
public static partial class BoundaryMessagingNetwork
{
}
