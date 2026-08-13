// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.MediatorFramework.Sample.Application.Exceptions;

/// <summary>Indicates that a book print process is not in a cancellable state.</summary>
public sealed class BookPrintProcessCannotBeCancelledViolation : BusinessRuleViolation
{
    /// <summary>Initializes a new instance of the <see cref="BookPrintProcessCannotBeCancelledViolation"/> class.</summary>
    /// <param name="processId">The identifier of the print process.</param>
    /// <param name="status">The current print-process status.</param>
    public BookPrintProcessCannotBeCancelledViolation(
        Guid processId,
        EvolvableEnum<BookPrintProcessStatus> status)
        : base("The book print process cannot be cancelled.")
    {
        ProcessId = processId;
        CurrentStatus = status;
        Detail = $"Book print process '{processId:D}' has status '{status}' and cannot be cancelled.";
    }

    /// <summary>Gets the identifier of the print process.</summary>
    public Guid ProcessId { get; }

    /// <summary>Gets the current print-process status.</summary>
    public EvolvableEnum<BookPrintProcessStatus> CurrentStatus { get; }
}
