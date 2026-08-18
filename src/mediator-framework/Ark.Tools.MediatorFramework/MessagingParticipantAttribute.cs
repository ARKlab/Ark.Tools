// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares the transport-neutral identity and subscriptions of one assembly participant.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class MessagingParticipantAttribute : Attribute
{
    /// <summary>Gets or sets the optional participant identity.</summary>
    public string? Identity { get; set; }

    /// <summary>Gets or sets the participant role.</summary>
    public MessagingParticipantRole Role { get; set; }

    /// <summary>Gets or sets the network profile referenced by the participant.</summary>
    public Type? Network { get; set; }

    /// <summary>Gets or sets the event contracts explicitly subscribed to by the participant.</summary>
    public Type[] Subscriptions { get; set; } = [];

    /// <summary>Gets or sets participant-local incoming pipeline step types.</summary>
    public Type[] IncomingSteps { get; set; } = [];

    /// <summary>Gets or sets participant-local outgoing pipeline step types.</summary>
    public Type[] OutgoingSteps { get; set; } = [];
}
