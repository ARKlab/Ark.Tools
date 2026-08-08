// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>SQL-backed <see cref="IBookStore"/> with one transaction per operation.</summary>
public sealed class SqlBookStore : IBookStore
{
    private readonly SampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="SqlBookStore"/> class.</summary>
    public SqlBookStore(SampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<BookResponse> CreateAsync(BookResponse book, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.SaveBookAsync(book, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public async Task<BookResponse> GetAsync(Guid id, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var book = await context.ReadBookAsync(id, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book ?? throw new EntityNotFoundException($"Book '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<BookResponse> UpdateAsync(BookResponse book, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.UpdateBookAsync(book, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");

        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.DeleteBookAsync(id, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{id}' was not found.");

        await context.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BookPage> SearchAsync(SearchBooksQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadBooksAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}
