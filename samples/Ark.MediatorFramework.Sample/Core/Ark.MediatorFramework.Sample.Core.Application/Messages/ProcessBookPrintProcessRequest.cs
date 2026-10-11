// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Core.Application.Messages;

/// <summary>Processes a queued book print request in the background.</summary>
[Message(
    Name = "books_process_book_print_process",
    FormerNames = new[]
    {
        "ark_mediator_framework_sample_application_messages_process_book_print_process_request",
    })]
public sealed record ProcessBookPrintProcessRequest :
    ICommand<ProcessBookPrintProcessRequest>
{
    /// <summary>Gets the print-process identifier.</summary>
    public BookPrintProcessId Id { get; init; }
}
