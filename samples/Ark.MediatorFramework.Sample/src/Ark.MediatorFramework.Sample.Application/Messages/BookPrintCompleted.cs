// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Notifies messaging participants that a book print completed.</summary>
[Event(Name = "books/book-print.completed")]
public sealed record BookPrintCompleted : ICommand<BookPrintCompleted>
{
    /// <summary>Gets the identifier of the printed book.</summary>
    public Guid BookId { get; init; }
}
