// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.


using Ark.Tools.Core;
using Ark.Tools.Outbox;

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Injects deterministic optimistic-concurrency failures for sample test scenarios.</summary>
public sealed class ConcurrencyFaultInjector
{
    private int _pendingFailures;

    /// <summary>Gets or sets the number of failures still to inject.</summary>
    public int PendingFailures
    {
        get => Volatile.Read(ref _pendingFailures);
        set => Volatile.Write(ref _pendingFailures, value);
    }

    /// <summary>Throws a synthetic optimistic-concurrency failure when one is pending.</summary>
    public void ThrowIfPending()
    {
        while (true)
        {
            var pending = Volatile.Read(ref _pendingFailures);
            if (pending <= 0)
                return;
            if (Interlocked.CompareExchange(ref _pendingFailures, pending - 1, pending) == pending)
                throw new OptimisticConcurrencyException("Synthetic optimistic-concurrency failure.");
        }
    }
}

/// <summary>Wraps a sample context factory and injects deterministic failures before greeting updates.</summary>
public sealed class FaultInjectingSampleDataContextFactory : ISampleDataContextFactory
{
    private readonly ISampleDataContextFactory _inner;
    private readonly ConcurrencyFaultInjector _faults;

    /// <summary>Initializes a new instance of the <see cref="FaultInjectingSampleDataContextFactory"/> class.</summary>
    public FaultInjectingSampleDataContextFactory(
        ISampleDataContextFactory inner,
        ConcurrencyFaultInjector faults)
    {
        _inner = inner;
        _faults = faults;
    }

    /// <inheritdoc />
    public async Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default)
    {
        var context = await _inner.CreateAsync(ctk).ConfigureAwait(false);
        return new Context(context, _faults);
    }

    async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await _inner.CreateAsync(ctk).ConfigureAwait(false);
    }

    private sealed class Context : ISampleDataContext
    {
        private readonly ISampleDataContext _inner;
        private readonly ConcurrencyFaultInjector _faults;

        public Context(ISampleDataContext inner, ConcurrencyFaultInjector faults)
        {
            _inner = inner;
            _faults = faults;
        }

        public IOutboxContextCore OutboxContext => _inner.OutboxContext;

        public async Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
        {
            await _inner.SaveAsync(greeting, ctk).ConfigureAwait(false);
        }

        public async Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default)
        {
            await _inner.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        }

        public async Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default)
        {
            return await _inner.ReadAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
        {
            return await _inner.ReadAllAsync(ctk).ConfigureAwait(false);
        }

        public async Task<GreetingResponse?> UpdateAsync(
            Guid id,
            string message,
            string eTag,
            Guid auditId,
            CancellationToken ctk = default)
        {
            _faults.ThrowIfPending();
            return await _inner.UpdateAsync(id, message, eTag, auditId, ctk).ConfigureAwait(false);
        }

        public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(
            GetAuditsQuery query,
            CancellationToken ctk = default)
        {
            return await _inner.ReadAuditsAsync(query, ctk).ConfigureAwait(false);
        }

        public async Task<GreetingPage> ReadGreetingsAsync(
            SearchGreetingsQuery query,
            CancellationToken ctk = default)
        {
            return await _inner.ReadGreetingsAsync(query, ctk).ConfigureAwait(false);
        }

        public async Task CommitAsync(CancellationToken ctk = default)
        {
            await _inner.CommitAsync(ctk).ConfigureAwait(false);
        }

        public async Task SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            await _inner.SaveBookAsync(book, ctk).ConfigureAwait(false);
        }

        public async Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default)
        {
            return await _inner.ReadBookAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            return await _inner.UpdateBookAsync(book, ctk).ConfigureAwait(false);
        }

        public async Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default)
        {
            return await _inner.DeleteBookAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<Book.V1.Page> ReadBooksAsync(
            Book_SearchQuery.V1 query,
            CancellationToken ctk = default)
        {
            return await _inner.ReadBooksAsync(query, ctk).ConfigureAwait(false);
        }

        public async Task<bool> TrySaveBookPrintProcessAsync(
            BookPrintProcessResponse process,
            CancellationToken ctk = default)
        {
            return await _inner.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(
            Guid id,
            CancellationToken ctk = default)
        {
            return await _inner.ReadBookPrintProcessAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookPrintProcessAsync(
            BookPrintProcessResponse process,
            CancellationToken ctk = default)
        {
            return await _inner.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse?> CancelBookPrintProcessAsync(
            Guid id,
            CancellationToken ctk = default)
        {
            return await _inner.CancelBookPrintProcessAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task CommitAsync(bool reuse, CancellationToken ctk = default)
        {
            await _inner.CommitAsync(reuse, ctk).ConfigureAwait(false);
        }

        public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            await _inner.SendAsync(messages, ctk).ConfigureAwait(false);
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(
            int messageCount = 10,
            CancellationToken ctk = default)
        {
            return await _inner.PeekLockMessagesAsync(messageCount, ctk).ConfigureAwait(false);
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            return await _inner.CountAsync(ctk).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken ctk = default)
        {
            await _inner.ClearAsync(ctk).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
    }
}
