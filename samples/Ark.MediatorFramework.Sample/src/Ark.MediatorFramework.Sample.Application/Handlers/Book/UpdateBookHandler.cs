// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Updates books through the application contract.</summary>
public sealed class UpdateBookHandler : IRequestHandler<Book_UpdateRequest.V1, Book.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="UpdateBookHandler"/> class.</summary>
    public UpdateBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> ExecuteAsync(Book_UpdateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var current = await context.ReadBookAsync(request.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{request.Id}' was not found.");
        var book = current with
        {
            Title = request.Data.Title,
            Author = request.Data.Author,
            Genre = request.Data.Genre,
            Description = $"Book updated: {request.Data.Title} by {request.Data.Author}",
        };
        if (!await context.UpdateBookAsync(book, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(Book.V1.Output),
            Identifier = book.Id.ToString("D"),
            Operation = typeof(Book_UpdateRequest).Name + "." + typeof(Book_UpdateRequest.V1).Name,
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}
