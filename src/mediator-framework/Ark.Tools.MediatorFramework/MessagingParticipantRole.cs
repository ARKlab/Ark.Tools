// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Identifies the behavior of an assembly-level messaging participant.</summary>
public enum MessagingParticipantRole
{
    /// <summary>The participant owns an identity queue and may subscribe to events.</summary>
    Consumer,

    /// <summary>The participant sends messages and may publish events it owns.</summary>
    Producer,
}
