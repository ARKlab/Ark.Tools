// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Outbox.Rebus;

using Microsoft.Data.SqlClient;

using Rebus.Bus;

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
    public async Task<BookResponse> CreateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
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
    public async Task<BookResponse> UpdateAsync(BookResponse book, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        if (!await context.UpdateBookAsync(book, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{book.Id}' was not found.");

        await context.CommitAsync(ctk).ConfigureAwait(false);
        return book;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
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
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        try
        {
            if (!await context.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
                return false;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return false;
        }
        using var scope = bus.Enlist(context);
        await bus.Send(new ProcessBookPrintProcessRequest { Id = process.Id }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> GetPrintProcessAsync(Guid id, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var process = await context.ReadBookPrintProcessAsync(id, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process ?? throw new EntityNotFoundException($"Book print process '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> UpdatePrintProcessAsync(
        BookPrintProcessResponse process,
        AuditEntry audit,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(audit);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        if (!await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");

        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }

}
