// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Outbox;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Creates shared in-memory contexts for handler-owned transactions.</summary>
public sealed class InMemorySampleDataContextFactory : ISampleDataContextFactory
{
    private readonly IGreetingStore _greetings;
    private readonly IAuditStore _audits;
    private readonly IBookStore _books;

    /// <summary>Initializes a new instance of the <see cref="InMemorySampleDataContextFactory"/> class.</summary>
    /// <param name="greetings">The shared in-memory greeting state.</param>
    /// <param name="audits">The shared in-memory audit state.</param>
    /// <param name="books">The shared in-memory book state.</param>
    public InMemorySampleDataContextFactory(IGreetingStore greetings, IAuditStore audits, IBookStore books)
    {
        _greetings = greetings;
        _audits = audits;
        _books = books;
    }

    /// <inheritdoc />
    public async Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return new Context(_greetings, _audits, _books);
    }

    private sealed class Context : ISampleDataContext
    {
        private readonly IGreetingStore _greetings;
        private readonly IAuditStore _audits;
        private readonly IBookStore _books;

        public Context(IGreetingStore greetings, IAuditStore audits, IBookStore books)
        {
            _greetings = greetings;
            _audits = audits;
            _books = books;
        }

        public IOutboxContextCore? OutboxContext => null;

        public Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
        {
            return _greetings.SaveAsync(greeting, ctk: ctk);
        }

        public Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default)
        {
            return _audits.WriteAsync(audit, ctk);
        }

        public Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default)
        {
            return _greetings.TryGetAsync(id, ctk);
        }

        public Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
        {
            return _greetings.AllAsync(ctk);
        }

        public async Task<GreetingResponse?> UpdateAsync(
            Guid id,
            string message,
            string eTag,
            Guid auditId,
            CancellationToken ctk = default)
        {
            try
            {
                return await _greetings.UpdateAsync(id, message, eTag, ctk: ctk).ConfigureAwait(false);
            }
            catch (Ark.Tools.Core.EntityTag.EntityTagMismatchException)
            {
                return null;
            }
        }

        public Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
        {
            return _audits.ReadAsync(query, ctk);
        }

        public Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
        {
            return _greetings.ReadGreetingsAsync(query, ctk);
        }

        public Task CommitAsync(CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveBookAsync(BookResponse book, CancellationToken ctk = default)
        {
            return _books.CreateAsync(book, ctk: ctk);
        }

        public Task<BookResponse?> ReadBookAsync(Guid id, CancellationToken ctk = default)
        {
            return ReadBookCoreAsync(id, ctk);
        }

        public async Task<bool> UpdateBookAsync(BookResponse book, CancellationToken ctk = default)
        {
            await _books.UpdateAsync(book, ctk: ctk).ConfigureAwait(false);
            return true;
        }

        public Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default)
        {
            return DeleteBookCoreAsync(id, ctk);
        }

        public Task<BookPage> ReadBooksAsync(SearchBooksQuery query, CancellationToken ctk = default)
        {
            return _books.SearchAsync(query, ctk);
        }

        public async Task<bool> TrySaveBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
        {
            return await _books.TryCreatePrintProcessAsync(process, ctk).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(Guid id, CancellationToken ctk = default)
        {
            return await ReadPrintProcessAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
        {
            return await UpdatePrintProcessAsync(process, ctk).ConfigureAwait(false);
        }

        private async Task<BookPrintProcessResponse?> ReadPrintProcessAsync(Guid id, CancellationToken ctk)
        {
            try
            {
                return await _books.GetPrintProcessAsync(id, ctk).ConfigureAwait(false);
            }
            catch (EntityNotFoundException)
            {
                return null;
            }
        }

        private async Task<bool> UpdatePrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk)
        {
            await _books.UpdatePrintProcessAsync(
                process,
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    EntityType = nameof(BookPrintProcessResponse),
                    Identifier = process.Id.ToString("D"),
                    Operation = nameof(UpdateBookPrintProcessAsync),
                    UserId = "context",
                    Timestamp = NodaTime.SystemClock.Instance.GetCurrentInstant(),
                },
                ctk).ConfigureAwait(false);
            return true;
        }

        private async Task<BookResponse?> ReadBookCoreAsync(Guid id, CancellationToken ctk)
        {
            try
            {
                return await _books.GetAsync(id, ctk).ConfigureAwait(false);
            }
            catch (Ark.Tools.Core.EntityNotFoundException)
            {
                return null;
            }
        }

        private async Task<bool> DeleteBookCoreAsync(Guid id, CancellationToken ctk)
        {
            await _books.DeleteAsync(id, ctk: ctk).ConfigureAwait(false);
            return true;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
