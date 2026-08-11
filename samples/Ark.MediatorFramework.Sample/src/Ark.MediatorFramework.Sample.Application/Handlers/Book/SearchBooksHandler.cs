// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Searches books through the application contract.</summary>
public sealed class SearchBooksHandler : IQueryHandler<Book_SearchQuery.V1, Book.V1.Page>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="SearchBooksHandler"/> class.</summary>
    public SearchBooksHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Page> ExecuteAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadBooksAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}
