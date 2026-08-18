// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Immutable metadata for one assembly-level messaging participant.</summary>
public sealed record MessagingParticipantDescriptor
{
    /// <summary>Creates participant metadata.</summary>
    public MessagingParticipantDescriptor(
        Type networkType,
        string? identity,
        MessagingParticipantRole role,
        IReadOnlyList<Type> subscriptions,
        IReadOnlyList<Type> incomingSteps,
        IReadOnlyList<Type> outgoingSteps,
        IReadOnlyList<Type> receivedMessages)
    {
        NetworkType = networkType ?? throw new ArgumentNullException(nameof(networkType));
        Identity = identity;
        Role = role;
        Subscriptions = (subscriptions ?? throw new ArgumentNullException(nameof(subscriptions))).ToArray();
        IncomingSteps = (incomingSteps ?? throw new ArgumentNullException(nameof(incomingSteps))).ToArray();
        OutgoingSteps = (outgoingSteps ?? throw new ArgumentNullException(nameof(outgoingSteps))).ToArray();
        ReceivedMessages = (receivedMessages ?? throw new ArgumentNullException(nameof(receivedMessages))).ToArray();
    }

    /// <summary>Referenced network profile.</summary>
    public Type NetworkType { get; }
    /// <summary>Participant identity, which is required for consumers and optional for producers.</summary>
    public string? Identity { get; }
    /// <summary>Participant role.</summary>
    public MessagingParticipantRole Role { get; }
    /// <summary>Explicit event subscriptions.</summary>
    public IReadOnlyList<Type> Subscriptions { get; }
    /// <summary>Incoming pipeline step types.</summary>
    public IReadOnlyList<Type> IncomingSteps { get; }
    /// <summary>Outgoing pipeline step types.</summary>
    public IReadOnlyList<Type> OutgoingSteps { get; }
    /// <summary>Messages automatically received by a named consumer identity.</summary>
    public IReadOnlyList<Type> ReceivedMessages { get; }
}
