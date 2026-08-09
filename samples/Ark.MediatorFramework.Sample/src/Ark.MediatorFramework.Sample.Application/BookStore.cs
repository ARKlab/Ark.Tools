// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using Rebus.Bus;

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Persists books for the sample application.</summary>
public interface IBookStore
{
    /// <summary>Creates a book.</summary>
    Task<Book.V1.Output> CreateAsync(Book.V1.Output book, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Reads a book by identifier.</summary>
    Task<Book.V1.Output> GetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book.</summary>
    Task<Book.V1.Output> UpdateAsync(Book.V1.Output book, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Deletes a book.</summary>
    Task DeleteAsync(Guid id, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Searches books.</summary>
    Task<Book.V1.Page> SearchAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default);

    /// <summary>Atomically creates and queues a book print process when the book has no active process.</summary>
    Task<bool> TryCreateAndQueuePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        IBus bus,
        CancellationToken ctk = default);

    /// <summary>Atomically creates a book print process when no active process exists.</summary>
    Task<bool> TryCreatePrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default);

    /// <summary>Reads a book print process.</summary>
    Task<BookPrintProcessResponse> GetPrintProcessAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book print process.</summary>
    Task<BookPrintProcessResponse> UpdatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default);

}

/// <summary>Thread-safe in-memory <see cref="IBookStore"/>.</summary>
public sealed class InMemoryBookStore : IBookStore
{
    private readonly ConcurrentDictionary<Guid, Book.V1.Output> _books = new();
    private readonly ConcurrentDictionary<Guid, BookPrintProcessResponse> _printProcesses = new();
    private readonly System.Threading.Lock _sync = new();
    private readonly IAuditStore _audits;

    /// <summary>Initializes a new instance of the in-memory book store.</summary>
    /// <param name="audits">The shared in-memory audit store.</param>
    public InMemoryBookStore(IAuditStore audits)
    {
        _audits = audits;
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> CreateAsync(Book.V1.Output book, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!_books.TryAdd(book.Id, book))
            throw new InvalidOperationException($"Book '{book.Id}' already exists.");

        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public Task<Book.V1.Output> GetAsync(Guid id, CancellationToken ctk = default)
    {
        return _books.TryGetValue(id, out var book)
            ? Task.FromResult(book)
            : throw new EntityNotFoundException($"Book '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<Book.V1.Output> UpdateAsync(Book.V1.Output book, AuditEntry? audit = null, CancellationToken ctk = default)
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
    public Task<Book.V1.Page> SearchAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var matching = _books.Values
            .Where(book =>
                (query.Title is null || string.Equals(book.Title, query.Title, StringComparison.Ordinal))
                && (query.Author is null || string.Equals(book.Author, query.Author, StringComparison.Ordinal))
                && (query.Genre is null || book.Genre == query.Genre))
            .OrderBy(book => book.Id)
            .ToArray();

        return Task.FromResult(new Book.V1.Page
        {
            Count = matching.LongLength,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = matching.Skip(query.Skip).Take(query.Limit).ToArray(),
        });
    }

    /// <inheritdoc />
    public async Task<bool> TryCreateAndQueuePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        IBus bus,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(bus);
        if (!await TryCreatePrintProcessAsync(process, ctk).ConfigureAwait(false))
            return false;

        try
        {
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
            await bus.Send(new ProcessBookPrintProcessRequest { Id = process.Id }).ConfigureAwait(false);
            return true;
        }
        catch
        {
            _printProcesses.TryRemove(process.Id, out _);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryCreatePrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        await Task.CompletedTask.ConfigureAwait(false);
        lock (_sync)
        {
            if (_printProcesses.Values.Any(item => item.BookId == process.BookId
                && (item.Status == BookPrintProcessStatus.Pending || item.Status == BookPrintProcessStatus.Running)))
                return false;
            if (!_printProcesses.TryAdd(process.Id, process))
                throw new InvalidOperationException($"Book print process '{process.Id}' already exists.");
        }

        return true;
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

}
