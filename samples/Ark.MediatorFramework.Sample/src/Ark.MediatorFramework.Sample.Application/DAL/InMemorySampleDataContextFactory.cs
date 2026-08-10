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
    private readonly ConcurrentDictionary<Guid, GreetingResponse> _greetings = new();
    private readonly ConcurrentDictionary<Guid, long> _greetingVersions = new();
    private readonly ConcurrentQueue<AuditRecord> _audits = new();
    private readonly ConcurrentDictionary<Guid, Book.V1.Output> _books = new();
    private readonly ConcurrentDictionary<Guid, BookPrintProcessResponse> _printProcesses = new();
    private readonly System.Threading.Lock _sync = new();
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
            _greetings.Clear();
            _greetingVersions.Clear();
            _audits.Clear();
            _books.Clear();
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

        public async Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(greeting);
            var version = _owner._greetingVersions.GetOrAdd(greeting.Id, 1);
            _owner._greetings[greeting.Id] = greeting with { ETag = $"0x{version:X16}" };
            await Task.CompletedTask.ConfigureAwait(false);
        }

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

        public async Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default)
        {
            _owner._greetings.TryGetValue(id, out var greeting);
            return await Task.FromResult(greeting).ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
        {
            return await Task.FromResult<IReadOnlyCollection<GreetingResponse>>(_owner._greetings.Values.ToArray())
                .ConfigureAwait(false);
        }

        public async Task<GreetingResponse?> UpdateAsync(
            Guid id,
            string message,
            string eTag,
            Guid auditId,
            CancellationToken ctk = default)
        {
            GreetingResponse? updated = null;
            lock (_owner._sync)
            {
                if (_owner._greetings.TryGetValue(id, out var current))
                {
                    var version = _owner._greetingVersions.GetOrAdd(id, 1);
                    if (string.Equals(eTag, $"0x{version:X16}", StringComparison.Ordinal))
                    {
                        updated = current with { Message = message, ETag = $"0x{version + 1:X16}", AuditId = auditId };
                        _owner._greetings[id] = updated;
                        _owner._greetingVersions[id] = version + 1;
                    }
                }
            }

            return await Task.FromResult(updated).ConfigureAwait(false);
        }

        public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(
            GetAuditsQuery query,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidateAuditSorts(query.Sort ?? []);
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

        public async Task<GreetingPage> ReadGreetingsAsync(
            SearchGreetingsQuery query,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            var matching = _owner._greetings.Values
                .Where(greeting => query.MessageContains is null
                    || greeting.Message.Contains(query.MessageContains, StringComparison.OrdinalIgnoreCase))
                .OrderBy(greeting => greeting.Id)
                .ToArray();
            return await Task.FromResult(new GreetingPage
            {
                Count = matching.Length,
                Skip = query.Skip,
                Limit = query.Limit,
                Data = matching.Skip(query.Skip).Take(query.Limit).ToArray(),
            }).ConfigureAwait(false);
        }

        public async Task CommitAsync(CancellationToken ctk = default)
        {
            await _outbox.CommitAsync(ctk).ConfigureAwait(false);
        }

        public async Task SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(book);
            if (!_owner._books.TryAdd(book.Id, book))
                throw new InvalidOperationException($"Book '{book.Id}' already exists.");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default)
        {
            _owner._books.TryGetValue(id, out var book);
            return await Task.FromResult(book).ConfigureAwait(false);
        }

        public async Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(book);
            var updated = _owner._books.ContainsKey(book.Id);
            if (updated)
                _owner._books[book.Id] = book;
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
                    && (query.Genre is null || book.Genre == query.Genre))
                .OrderBy(book => book.Id)
                .ToArray();
            return await Task.FromResult(new Book.V1.Page
            {
                Count = matching.LongLength,
                Skip = query.Skip,
                Limit = query.Limit,
                Data = matching.Skip(query.Skip).Take(query.Limit).ToArray(),
            }).ConfigureAwait(false);
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
            var updated = _owner._printProcesses.ContainsKey(process.Id);
            if (updated)
                _owner._printProcesses[process.Id] = process;
            return await Task.FromResult(updated).ConfigureAwait(false);
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

        private static void ValidateAuditSorts(IEnumerable<string> sorts)
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
    }
}
