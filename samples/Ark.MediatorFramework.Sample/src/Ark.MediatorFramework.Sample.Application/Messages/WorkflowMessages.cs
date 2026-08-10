// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Rebus-only request completed asynchronously by the composition workflow.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record CompleteGreetingCompositionRequest : IRequest<CompleteGreetingCompositionRequest, GreetingResponse>
{
    /// <summary>Gets the greeting identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name to greet.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the number of transient processing failures to simulate before completing the workflow.</summary>
    public int FailuresBeforeSuccess { get; init; }
}

/// <summary>Notification emitted by the SQL transaction after a greeting is persisted.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record GreetingCreatedNotification : ICommand<GreetingCreatedNotification>
{
    /// <summary>Gets the persisted greeting.</summary>
    public required GreetingResponse Greeting { get; init; }
}

/// <summary>Processes a queued book print request in the background.</summary>
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record ProcessBookPrintProcessRequest :
    IRequest<ProcessBookPrintProcessRequest, BookPrintProcessResponse>
{
    /// <summary>Gets the print-process identifier.</summary>
    public Guid Id { get; init; }
}
