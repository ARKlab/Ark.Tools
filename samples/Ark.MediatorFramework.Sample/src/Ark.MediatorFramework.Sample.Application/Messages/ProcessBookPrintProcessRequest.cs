// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Processes a queued book print request in the background.</summary>
[Message]
[RebusMessage(OwnerQueue = "ark-mediator-sample")]
public sealed record ProcessBookPrintProcessRequest :
    IRequest<ProcessBookPrintProcessRequest, BookPrintProcessResponse>,
    ICommand<ProcessBookPrintProcessRequest>
{
    /// <summary>Gets the print-process identifier.</summary>
    public Guid Id { get; init; }
}
