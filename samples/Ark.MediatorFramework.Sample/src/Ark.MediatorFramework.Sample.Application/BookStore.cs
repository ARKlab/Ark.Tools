// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Persists books for the sample application.</summary>
public interface IBookStore
{
    /// <summary>Creates a book.</summary>
    Task<BookResponse> CreateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Reads a book by identifier.</summary>
    Task<BookResponse> GetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book.</summary>
    Task<BookResponse> UpdateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Deletes a book.</summary>
    Task DeleteAsync(Guid id, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Searches books.</summary>
    Task<BookPage> SearchAsync(SearchBooksQuery query, CancellationToken ctk = default);

    /// <summary>Creates a book print process.</summary>
    Task<BookPrintProcessResponse> CreatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default);

    /// <summary>Reads a book print process.</summary>
    Task<BookPrintProcessResponse> GetPrintProcessAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book print process.</summary>
    Task<BookPrintProcessResponse> UpdatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default);

    /// <summary>Gets whether a book has a pending or running print process.</summary>
    Task<bool> HasActivePrintProcessAsync(Guid bookId, CancellationToken ctk = default);
}

/// <summary>Thread-safe in-memory <see cref="IBookStore"/>.</summary>
public sealed class InMemoryBookStore : IBookStore
{
    private readonly ConcurrentDictionary<Guid, BookResponse> _books = new();
    private readonly ConcurrentDictionary<Guid, BookPrintProcessResponse> _printProcesses = new();
    private readonly IAuditStore _audits;

    /// <summary>Initializes a new instance of the in-memory book store.</summary>
    /// <param name="audits">The shared in-memory audit store.</param>
    public InMemoryBookStore(IAuditStore audits)
    {
        _audits = audits;
    }

    /// <inheritdoc />
    public async Task<BookResponse> CreateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!_books.TryAdd(book.Id, book))
            throw new InvalidOperationException($"Book '{book.Id}' already exists.");

        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public Task<BookResponse> GetAsync(Guid id, CancellationToken ctk = default)
    {
        return _books.TryGetValue(id, out var book)
            ? Task.FromResult(book)
            : throw new EntityNotFoundException($"Book '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<BookResponse> UpdateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!_books.ContainsKey(book.Id))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");

        _books[book.Id] = book;
        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        if (!_books.TryRemove(id, out _))
            throw new EntityNotFoundException($"Book '{id}' was not found.");

        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<BookPage> SearchAsync(SearchBooksQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var matching = _books.Values
            .Where(book =>
                (query.Title is null || string.Equals(book.Title, query.Title, StringComparison.Ordinal))
                && (query.Author is null || string.Equals(book.Author, query.Author, StringComparison.Ordinal))
                && (query.Genre is null || book.Genre == query.Genre))
            .OrderBy(book => book.Id)
            .ToArray();

        return Task.FromResult(new BookPage
        {
            Count = matching.LongLength,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = matching.Skip(query.Skip).Take(query.Limit).ToArray(),
        });
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> CreatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(audit);
        if (!_printProcesses.TryAdd(process.Id, process))
            throw new InvalidOperationException($"Book print process '{process.Id}' already exists.");

        await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return process;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> GetPrintProcessAsync(Guid id, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return _printProcesses.TryGetValue(id, out var process)
            ? process
            : throw new EntityNotFoundException($"Book print process '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> UpdatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(audit);
        if (!_printProcesses.ContainsKey(process.Id))
            throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");

        _printProcesses[process.Id] = process;
        await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return process;
    }

    /// <inheritdoc />
    public async Task<bool> HasActivePrintProcessAsync(Guid bookId, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return _printProcesses.Values.Any(process => process.BookId == bookId
            && (process.Status == BookPrintProcessStatus.Pending || process.Status == BookPrintProcessStatus.Running));
    }
}
