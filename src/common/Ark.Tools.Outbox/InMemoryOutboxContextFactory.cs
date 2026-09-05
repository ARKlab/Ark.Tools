// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Outbox;

/// <summary>
/// Provides composable in-memory outbox contexts for tests and local development.
/// </summary>
public sealed class InMemoryOutboxContextFactory : IOutboxContextFactory, IOutboxAsyncContextFactory
{
    private readonly InMemoryOutbox _outbox;

    /// <summary>Initializes an empty in-memory outbox.</summary>
    public InMemoryOutboxContextFactory()
        : this(new InMemoryOutbox())
    {
    }

    /// <summary>Initializes an in-memory outbox using the supplied shared state.</summary>
    /// <param name="outbox">The shared outbox state.</param>
    public InMemoryOutboxContextFactory(InMemoryOutbox outbox)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    /// <inheritdoc />
    public IOutboxContext Create()
    {
        return new Context(_outbox);
    }

    /// <inheritdoc />
    public async Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        ctk.ThrowIfCancellationRequested();
        return new Context(_outbox);
    }

    private sealed class Context : IOutboxContext, IOutboxAsyncContext
    {
        private readonly InMemoryOutbox _outbox;
        private readonly object _owner = new();
        private readonly List<OutboxMessage> _messages = [];
        private readonly List<long> _lockedMessageIds = [];
        private bool _clearRequested;
        private bool _completed;
        private bool _disposed;

        internal Context(InMemoryOutbox outbox)
        {
            _outbox = outbox;
        }

        public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            _ensureActive();
            ArgumentNullException.ThrowIfNull(messages);
            ctk.ThrowIfCancellationRequested();
            foreach (var message in messages)
            {
                ArgumentNullException.ThrowIfNull(message);
                _messages.Add(_clone(message));
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(
            int messageCount = 10,
            CancellationToken ctk = default)
        {
            _ensureActive();
            ArgumentOutOfRangeException.ThrowIfNegative(messageCount);
            ctk.ThrowIfCancellationRequested();
            var messages = _clearRequested
                ? []
                : _outbox._peekLock(_owner, messageCount, _lockedMessageIds);

            await Task.CompletedTask.ConfigureAwait(false);
            return messages;
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            _ensureActive();
            ctk.ThrowIfCancellationRequested();
            var count = _clearRequested ? _messages.Count : _outbox._count() + _messages.Count;

            await Task.CompletedTask.ConfigureAwait(false);
            return count;
        }

        public async Task ClearAsync(CancellationToken ctk = default)
        {
            _ensureActive();
            ctk.ThrowIfCancellationRequested();
            _messages.Clear();
            _clearRequested = true;

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Commit()
        {
            _ensureActive();
            _commitCore();
        }

        private void _commitCore()
        {
            _outbox._commit(_owner, _messages, _lockedMessageIds, _clearRequested);
            _messages.Clear();
            _lockedMessageIds.Clear();
            _clearRequested = false;
            _completed = true;
        }

        public async Task CommitAsync(CancellationToken ctk = default)
        {
            _ensureActive();
            await CommitAsync(false, ctk).ConfigureAwait(false);
        }

        public async Task CommitAsync(bool reuse, CancellationToken ctk = default)
        {
            _ensureActive();
            ctk.ThrowIfCancellationRequested();
            _commitCore();
            if (reuse)
                _completed = false;

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _outbox._release(_owner, _lockedMessageIds);
                _disposed = true;
            }

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _outbox._release(_owner, _lockedMessageIds);
            _disposed = true;
        }

        private void _ensureActive()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
                throw new InvalidOperationException("The outbox context has already been committed.");
        }

        private static OutboxMessage _clone(OutboxMessage message)
        {
            return new OutboxMessage
            {
                Body = message.Body is null ? null : [.. message.Body],
                Headers = message.Headers is null
                    ? null
                    : new Dictionary<string, string>(message.Headers, StringComparer.Ordinal),
            };
        }
    }
}

/// <summary>
/// Thread-safe shared state used by <see cref="InMemoryOutboxContextFactory"/>.
/// </summary>
public sealed class InMemoryOutbox
{
#pragma warning disable MA0158 // object lock is required for net8.0 compatibility
    private readonly object _sync = new();
#pragma warning restore MA0158
    private readonly List<Entry> _entries = [];
    private long _nextId;

    internal IReadOnlyList<OutboxMessage> _peekLock(object owner, int messageCount, ICollection<long> lockedMessageIds)
    {
        lock (_sync)
        {
            var messages = new List<OutboxMessage>(messageCount);
            foreach (var entry in _entries.Where(static entry => entry.Owner is null).Take(messageCount))
            {
                entry.Owner = owner;
                lockedMessageIds.Add(entry.Id);
                messages.Add(_clone(entry.Message));
            }

            return messages;
        }
    }

    internal int _count()
    {
        lock (_sync)
        {
            return _entries.Count(static entry => entry.Owner is null);
        }
    }

    internal void _commit(
        object owner,
        IEnumerable<OutboxMessage> messages,
        IEnumerable<long> lockedMessageIds,
        bool clearRequested)
    {
        lock (_sync)
        {
            if (clearRequested)
                _entries.Clear();
            else
                _entries.RemoveAll(entry => entry.Owner == owner && lockedMessageIds.Contains(entry.Id));

            foreach (var message in messages)
            {
                _entries.Add(new Entry(++_nextId, _clone(message)));
            }
        }
    }

    internal void _release(object owner, IEnumerable<long> lockedMessageIds)
    {
        lock (_sync)
        {
            var ids = lockedMessageIds.ToHashSet();
            foreach (var entry in _entries)
            {
                if (entry.Owner == owner && ids.Contains(entry.Id))
                    entry.Owner = null;
            }
        }
    }

    private static OutboxMessage _clone(OutboxMessage message)
    {
        return new OutboxMessage
        {
            Body = message.Body is null ? null : [.. message.Body],
            Headers = message.Headers is null
                ? null
                : new Dictionary<string, string>(message.Headers, StringComparer.Ordinal),
        };
    }

    private sealed class Entry
    {
        public Entry(long id, OutboxMessage message)
        {
            Id = id;
            Message = message;
        }

        public long Id { get; }
        public OutboxMessage Message { get; }
        public object? Owner { get; set; }
    }
}
