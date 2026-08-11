// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

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
        var book = CreateResponse(Guid.NewGuid(), request.Data.Title, request.Data.Author, request.Data.Genre, request.Data.ISBN);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(CreateAudit(book.Id, typeof(Book_CreateRequest).Name + "." + typeof(Book_CreateRequest.V1).Name), ctk).ConfigureAwait(false);
        await context.SaveBookAsync(book, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    internal static Book.V1.Output CreateResponse(
        Guid id,
        string title,
        string author,
        EvolvableEnum<Book.V1.Genre> genre,
        string? isbn)
    {
        return new Book.V1.Output
        {
            Id = id,
            Title = title,
            Author = author,
            Genre = genre,
            ISBN = isbn,
            Description = $"Book created: {title} by {author}",
        };
    }

    private AuditEntry CreateAudit(Guid id, string operation)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(Book.V1.Output),
            Identifier = id.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}
