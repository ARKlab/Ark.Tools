// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Indicates that a book already has a pending or running print process.</summary>
public sealed class BookPrintingProcessAlreadyRunningViolation : BusinessRuleViolation
{
    /// <summary>Initializes a new instance of the <see cref="BookPrintingProcessAlreadyRunningViolation"/> class.</summary>
    /// <param name="bookId">The identifier of the book that already has a print process.</param>
    public BookPrintingProcessAlreadyRunningViolation(Guid bookId)
        : base("A book print process is already pending or running.")
    {
        BookId = bookId;
        Detail = $"Book '{bookId:D}' already has a pending or running print process.";
    }

    /// <summary>Gets the identifier of the book that already has a print process.</summary>
    public Guid BookId { get; }
}
