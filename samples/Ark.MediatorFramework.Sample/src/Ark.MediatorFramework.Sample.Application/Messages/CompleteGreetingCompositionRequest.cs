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
