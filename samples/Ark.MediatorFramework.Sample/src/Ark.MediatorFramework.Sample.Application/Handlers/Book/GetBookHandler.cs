// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Reads books through the application contract.</summary>
public sealed class GetBookHandler : IQueryHandler<Book_GetQuery.V1, Book.V1.Output>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetBookHandler"/> class.</summary>
    public GetBookHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> ExecuteAsync(Book_GetQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var book = await context.ReadBookAsync(query.Id, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }
}
