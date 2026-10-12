// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.Tools.MediatorFramework;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;

using Microsoft.Extensions.Logging;

using NodaTime;

namespace Ark.MediatorFramework.AotSample.Core.Application.Handlers;

/// <summary>Stores a book and, in the same transaction, queues its print command in the outbox.</summary>
public sealed class Book_CreateRequestHandler(AotDataContextFactory factory, IBus bus) : IRequestHandler<Book_CreateRequest.V1, Book>
{
    /// <inheritdoc />
    public async Task<Book> ExecuteAsync(Book_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await factory.OpenAsync(ctk).ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        var book = new Book
        {
            Id = BookId.From(Guid.NewGuid()),
            Title = request.Title,
            Genre = request.Genre,
            PublishedOn = request.PublishedOn,
            ReleasedAt = request.ReleasedAt,
            PrintSlot = request.PrintSlot,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            Contact = request.Contact,
        };
        await context.InsertBookAsync(book, ctk).ConfigureAwait(false);
        using var scope = new OutboxScope(context);
        await bus.Send(new PrintBookCommand { BookId = book.Id }, cancellationToken: ctk).ConfigureAwait(false);
        await scope.CompleteAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}

/// <summary>Reads a book.</summary>
public sealed class Book_GetQueryHandler(AotDataContextFactory factory) : IQueryHandler<Book_GetQuery.V1, Book?>
{
    /// <inheritdoc />
    public async Task<Book?> ExecuteAsync(Book_GetQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = await factory.OpenAsync(ctk).ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        // A null result answers 404.
        return await context.ReadBookAsync(query.Id, ctk).ConfigureAwait(false);
    }
}

/// <summary>Prints a book: marks it printed.</summary>
public sealed partial class PrintBookCommandHandler(AotDataContextFactory factory, ILogger<PrintBookCommandHandler> logger) : ICommandHandler<PrintBookCommand>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(PrintBookCommand command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = await factory.OpenAsync(ctk).ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        if (!await context.MarkPrintedAsync(command.BookId, SystemClock.Instance.GetCurrentInstant().InUtc().LocalDateTime, ctk).ConfigureAwait(false))
            throw new InvalidOperationException("Book " + command.BookId.Value.ToString("D", CultureInfo.InvariantCulture) + " does not exist.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        _logPrinted(logger, command.BookId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Printed book {BookId}")]
    private static partial void _logPrinted(ILogger logger, BookId bookId);
}
