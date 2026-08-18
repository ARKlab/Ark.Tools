// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Describes optional capabilities required by a messaging network.
/// Sending is implicit and is therefore not represented by a flag.
/// </summary>
[Flags]
public enum MessagingCapabilities
{
    /// <summary>No optional capability is required.</summary>
    None = 0,

    /// <summary>Participants can receive and settle messages.</summary>
    Receive = 1,

    /// <summary>Events can be published and subscribed to.</summary>
    PubSub = 2,

    /// <summary>Messages can be scheduled for later delivery.</summary>
    ScheduledSend = 4,
}
