// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Core.Reflection;
using Ark.Tools.Outbox;

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application.DAL;

/// <summary>Creates shared in-memory contexts for handler-owned transactions.</summary>
public sealed class InMemorySampleDataContextFactory : ISampleDataContextFactory
{
    private readonly ConcurrentQueue<AuditRecord> _audits = new();
    private readonly ConcurrentDictionary<Guid, Book.V1.Output> _books = new();
    private readonly ConcurrentDictionary<Guid, long> _bookVersions = new();
    private readonly ConcurrentDictionary<Guid, BookReview> _bookReviews = new();
    private readonly ConcurrentDictionary<Guid, ReadingActivity> _readingActivities = new();
    private readonly ConcurrentDictionary<Guid, BookPrintProcessResponse> _printProcesses = new();
    private readonly Lock _sync = new();
    private readonly IOutboxAsyncContextFactory _outboxFactory;

    /// <summary>Initializes a new instance of the <see cref="InMemorySampleDataContextFactory"/> class.</summary>
    /// <param name="outboxFactory">The shared in-memory outbox factory.</param>
    public InMemorySampleDataContextFactory(IOutboxAsyncContextFactory outboxFactory)
    {
        _outboxFactory = outboxFactory;
    }

    /// <summary>Removes all sample data from this factory.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _audits.Clear();
            _books.Clear();
            _bookVersions.Clear();
            _bookReviews.Clear();
            _readingActivities.Clear();
            _printProcesses.Clear();
        }
    }

    /// <inheritdoc />
    public async Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default)
    {
        var outbox = await _outboxFactory.CreateAsync(ctk).ConfigureAwait(false);
        return new Context(this, outbox);
    }

    async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await _outboxFactory.CreateAsync(ctk).ConfigureAwait(false);
    }

    private sealed class Context : ISampleDataContext
    {
        private readonly InMemorySampleDataContextFactory _owner;
        private readonly IOutboxAsyncContext _outbox;

        public Context(InMemorySampleDataContextFactory owner, IOutboxAsyncContext outbox)
        {
            _owner = owner;
            _outbox = outbox;
        }

        public IOutboxContextCore OutboxContext => _outbox;

        public async Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(audit);
            _owner._audits.Enqueue(new AuditRecord
            {
                Id = audit.Id,
                UserId = audit.UserId,
                EntityType = audit.EntityType,
                Identifier = audit.Identifier,
                Operation = audit.Operation,
                Timestamp = audit.Timestamp,
            });
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(
            GetAuditsQuery query,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            _validateAuditSorts(query.Sort ?? []);
            var filtered = _owner._audits.Where(record =>
                (query.UserId is null || record.UserId == query.UserId)
                && (query.EntityType is null || record.EntityType == query.EntityType)
                && (query.Identifier is null || record.Identifier == query.Identifier)
                && (query.FromTimestamp is null || record.Timestamp >= query.FromTimestamp.Value)
                && (query.ToTimestamp is null || record.Timestamp <= query.ToTimestamp.Value));
            var sorts = query.Sort ?? [];
            var ordered = sorts.Any()
                ? filtered.OrderBy(string.Join(", ", sorts))
                : filtered.OrderByDescending(record => record.Timestamp);
            var records = ordered.ToArray();
            return await Task.FromResult(new PagedResult<AuditRecord>
            {
                Count = records.LongLength,
                Skip = query.Skip,
                Limit = query.Limit,
                Data = records.Skip(query.Skip).Take(query.Limit).ToArray(),
            }).ConfigureAwait(false);
        }

        public async Task CommitAsync(CancellationToken ctk = default)
        {
            await _outbox.CommitAsync(ctk).ConfigureAwait(false);
        }

        public async Task<Book.V1.Output> SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(book);
            var stored = book with { ETag = "0x0000000000000001" };
            lock (_owner._sync)
            {
                if (!_owner._books.TryAdd(book.Id, stored))
                    throw new InvalidOperationException($"Book '{book.Id}' already exists.");
                _owner._bookVersions[book.Id] = 1;
            }
            return await Task.FromResult(stored).ConfigureAwait(false);
        }

        public async Task<IEnumerable<Book.V1.Output>> BulkInsertBooksAsync(
            IEnumerable<Book.V1.Output> books,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(books);
            var stored = books
                .Select(static book => book with { ETag = "0x0000000000000001" })
                .ToArray();
            lock (_owner._sync)
            {
                if (stored.GroupBy(book => book.Id).Any(group => group.Count() > 1)
                    || stored.Any(book => _owner._books.ContainsKey(book.Id)))
                    throw new InvalidOperationException("A book in the bulk request already exists.");
                foreach (var book in stored)
                {
                    _owner._books[book.Id] = book;
                    _owner._bookVersions[book.Id] = 1;
                }
            }
            return await Task.FromResult<IEnumerable<Book.V1.Output>>(stored).ConfigureAwait(false);
        }

        public async Task<Book.V1.Output?> ReadBookAsync(
            Guid id,
            CancellationToken ctk = default)
        {
            _owner._books.TryGetValue(id, out var book);
            return await Task.FromResult(book).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(book);
            var updated = false;
            lock (_owner._sync)
            {
                if (_owner._books.TryGetValue(book.Id, out var current)
                    && string.Equals(current.ETag, book.ETag, StringComparison.Ordinal))
                {
                    var version = _owner._bookVersions[book.Id] + 1;
                    _owner._bookVersions[book.Id] = version;
                    _owner._books[book.Id] = book with { ETag = $"0x{version:X16}" };
                    updated = true;
                }
            }
            return await Task.FromResult(updated).ConfigureAwait(false);
        }

        public async Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default)
        {
            return await Task.FromResult(_owner._books.TryRemove(id, out _)).ConfigureAwait(false);
        }

        public async Task<Book.V1.Page> ReadBooksAsync(
            Book_SearchQuery.V1 query,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            var matching = _owner._books.Values
                .Where(book =>
                    (query.Title is null || string.Equals(book.Title, query.Title, StringComparison.Ordinal))
                    && (query.Author is null || string.Equals(book.Author, query.Author, StringComparison.Ordinal))
                    && (query.Genre is null || book.Genre == query.Genre));
            var sorts = query.Sort?.Where(sort => !string.IsNullOrWhiteSpace(sort)).ToArray() ?? [];
            var ordered = sorts.Length == 0
                ? matching.OrderBy(book => book.Id)
                : matching.OrderBy(string.Join(", ", sorts));
            var results = ordered.ToArray();
            return await Task.FromResult(new Book.V1.Page
            {
                Count = results.LongLength,
                Skip = query.Skip,
                Limit = query.Limit,
                Data = results.Skip(query.Skip).Take(query.Limit).ToArray(),
            }).ConfigureAwait(false);
        }

        public async Task SaveBookReviewAsync(BookReview review, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(review);
            if (!_owner._bookReviews.TryAdd(review.Id, review))
                throw new InvalidOperationException($"Book review '{review.Id}' already exists.");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<BookReview>> ReadBookReviewsAsync(
            Guid bookId,
            int skip,
            int limit,
            CancellationToken ctk = default)
        {
            var reviews = _owner._bookReviews.Values
                .Where(review => review.BookId == bookId)
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.Id)
                .Skip(skip)
                .Take(limit)
                .ToArray();
            return await Task.FromResult<IReadOnlyList<BookReview>>(reviews).ConfigureAwait(false);
        }

        public async Task SaveReadingActivityAsync(ReadingActivity activity, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(activity);
            if (!_owner._readingActivities.TryAdd(activity.Id, activity))
                throw new InvalidOperationException($"Reading activity '{activity.Id}' already exists.");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ReadingActivity>> ReadReadingActivityAsync(
            Guid bookId,
            string userId,
            int limit,
            CancellationToken ctk = default)
        {
            var activities = _owner._readingActivities.Values
                .Where(activity => activity.BookId == bookId && activity.UserId == userId)
                .OrderByDescending(activity => activity.OccurredAt)
                .ThenByDescending(activity => activity.Id)
                .Take(limit)
                .ToArray();
            return await Task.FromResult<IReadOnlyList<ReadingActivity>>(activities).ConfigureAwait(false);
        }

        public async Task<bool> TrySaveBookPrintProcessAsync(
            BookPrintProcessResponse process,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(process);
            lock (_owner._sync)
            {
                if (_owner._printProcesses.Values.Any(item => item.BookId == process.BookId
                    && (item.Status == BookPrintProcessStatus.Pending || item.Status == BookPrintProcessStatus.Running)))
                    return false;
                if (!_owner._printProcesses.TryAdd(process.Id, process))
                    throw new InvalidOperationException($"Book print process '{process.Id}' already exists.");
            }

            return await Task.FromResult(true).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(
            Guid id,
            bool forUpdate = false,
            CancellationToken ctk = default)
        {
            _owner._printProcesses.TryGetValue(id, out var process);
            return await Task.FromResult(process).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookPrintProcessAsync(
            BookPrintProcessResponse process,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(process);
            var updated = false;
            lock (_owner._sync)
            {
                if (_owner._printProcesses.TryGetValue(process.Id, out var current)
                    && _canUpdate(current.Status, process.Status))
                {
                    _owner._printProcesses[process.Id] = process;
                    updated = true;
                }
            }

            return await Task.FromResult(updated).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse?> CancelBookPrintProcessAsync(
            Guid id,
            CancellationToken ctk = default)
        {
            BookPrintProcessResponse? cancelled = null;
            lock (_owner._sync)
            {
                if (_owner._printProcesses.TryGetValue(id, out var current)
                    && (current.Status == BookPrintProcessStatus.Pending
                        || current.Status == BookPrintProcessStatus.Running))
                {
                    cancelled = current with { Status = BookPrintProcessStatus.Cancelled };
                    _owner._printProcesses[id] = cancelled;
                }
            }

            return await Task.FromResult(cancelled).ConfigureAwait(false);
        }

        public async Task CommitAsync(bool reuse, CancellationToken ctk = default)
        {
            await _outbox.CommitAsync(reuse, ctk).ConfigureAwait(false);
        }

        public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            await _outbox.SendAsync(messages, ctk).ConfigureAwait(false);
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(
            int messageCount = 10,
            CancellationToken ctk = default)
        {
            return await _outbox.PeekLockMessagesAsync(messageCount, ctk).ConfigureAwait(false);
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            return await _outbox.CountAsync(ctk).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken ctk = default)
        {
            await _outbox.ClearAsync(ctk).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            return _outbox.DisposeAsync();
        }

        private static void _validateAuditSorts(IEnumerable<string> sorts)
        {
            var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                nameof(AuditRecord.Id),
                nameof(AuditRecord.UserId),
                nameof(AuditRecord.EntityType),
                nameof(AuditRecord.Identifier),
                nameof(AuditRecord.Operation),
                nameof(AuditRecord.Timestamp),
            };
            foreach (var sort in sorts.Where(sort => !string.IsNullOrWhiteSpace(sort)))
            {
                var parts = sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2 || !properties.Contains(parts[0]))
                    throw new ArgumentException($"Invalid audit sort '{sort}'.", nameof(sorts));
                if (parts.Length == 2
                    && !parts[1].Equals("ASC", StringComparison.OrdinalIgnoreCase)
                    && !parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Invalid audit sort direction '{parts[1]}'.", nameof(sorts));
            }
        }

        private static bool _canUpdate(
            EvolvableEnum<BookPrintProcessStatus> current,
            EvolvableEnum<BookPrintProcessStatus> next)
        {
            return (next == BookPrintProcessStatus.Running && current == BookPrintProcessStatus.Pending)
                || (next == BookPrintProcessStatus.Completed && current == BookPrintProcessStatus.Running)
                || (next == BookPrintProcessStatus.Error
                    && (current == BookPrintProcessStatus.Running || current == BookPrintProcessStatus.Completed));
        }
    }
}
