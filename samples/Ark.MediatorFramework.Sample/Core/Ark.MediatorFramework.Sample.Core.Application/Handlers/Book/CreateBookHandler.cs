// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Application.Handlers;

/// <summary>Creates books through the application contract.</summary>
public sealed class CreateBookHandler : IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookHandler"/> class.</summary>
    public CreateBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> ExecuteAsync(Book_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = _createResponse(BookId.New(), request.Data.Title, request.Data.Author, request.Data.Genre, request.Data.ISBN);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        await context.WriteAuditAsync(_createAudit(book.Id, typeof(Book_CreateRequest).Name + "." + typeof(Book_CreateRequest.V1).Name), ctk).ConfigureAwait(false);
        book = await context.SaveBookAsync(book, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    internal static Book.V1.Output _createResponse(
        BookId id,
        BookTitle title,
        PersonName author,
        EvolvableEnum<Book.V1.Genre> genre,
        Isbn? isbn)
    {
        return new Book.V1.Output
        {
            Id = id,
            Title = title,
            Author = author,
            Genre = genre,
            ISBN = isbn,
            // The author is personal data: it stays out of the description, which is not classified.
            Description = BookDescription.From($"Book created: {title.Value}"),
        };
    }

    private AuditEntry _createAudit(BookId id, string operation)
    {
        return new AuditEntry
        {
            UserId = _user.GetUserIdOrAnonymous(),
            EntityType = nameof(Book.V1.Output),
            Identifier = id.Value.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}
