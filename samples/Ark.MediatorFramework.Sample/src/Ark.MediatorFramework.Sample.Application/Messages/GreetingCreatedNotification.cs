// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Notification emitted by the SQL transaction after a greeting is persisted.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record GreetingCreatedNotification : ICommand<GreetingCreatedNotification>
{
    /// <summary>Gets the persisted greeting.</summary>
    public required GreetingResponse Greeting { get; init; }
}
