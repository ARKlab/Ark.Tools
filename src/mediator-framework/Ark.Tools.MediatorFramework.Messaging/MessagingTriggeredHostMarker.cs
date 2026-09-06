// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Marks a service collection as belonging to a host that owns triggering itself.</summary>
/// <remarks>
/// A triggered host — Azure Functions today — receives and settles messages through its own
/// trigger, so the components that own a receive loop (the processor host and the native outbox
/// processor) must refuse to compose alongside it. The marker lives here rather than in the
/// triggered host's package because the dependency runs the other way: the trigger integration
/// references this package, never the reverse.
/// </remarks>
public sealed class MessagingTriggeredHostMarker
{
    /// <summary>Gets the singleton marker instance.</summary>
    public static MessagingTriggeredHostMarker Instance { get; } = new();

    private MessagingTriggeredHostMarker()
    {
    }
}
