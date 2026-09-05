// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Creates multiple books through the application contract.</summary>
public sealed class BulkCreateBookHandler :
    IRequestHandler<Book_BulkCreateRequest.V1, IReadOnlyList<Book.V1.Output>>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="BulkCreateBookHandler"/> class.</summary>
    public BulkCreateBookHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Book.V1.Output>> ExecuteAsync(
        Book_BulkCreateRequest.V1 request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var books = request.Data
            .Select(static data => CreateBookHandler._createResponse(
                Guid.NewGuid(),
                data.Title,
                data.Author,
                data.Genre,
                data.ISBN))
            .ToArray();
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        foreach (var book in books)
        {
            await context.WriteAuditAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                UserId = _user.GetUserId() ?? "anonymous",
                EntityType = nameof(Book.V1.Output),
                Identifier = book.Id.ToString("D"),
                Operation = typeof(Book_BulkCreateRequest).Name + "." + typeof(Book_BulkCreateRequest.V1).Name,
                Timestamp = _clock.GetCurrentInstant(),
            }, ctk).ConfigureAwait(false);
        }
        var created = await context.BulkInsertBooksAsync(books, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return created.ToArray();
    }
}
