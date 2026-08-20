// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Declares optional capabilities required by a messaging network.</summary>
[Flags]
public enum MessagingCapabilities
{
    /// <summary>No optional capabilities.</summary>
    None = 0,

    /// <summary>Participants can receive messages.</summary>
    Receive = 1,

    /// <summary>The network supports event publication and subscriptions.</summary>
    PubSub = 2,

    /// <summary>The network supports delayed sends.</summary>
    ScheduledSend = 4
}
