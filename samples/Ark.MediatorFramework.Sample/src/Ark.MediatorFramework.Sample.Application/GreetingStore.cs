// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

using Ark.Tools.Core;
using Ark.Tools.Core.Reflection;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>In-memory store shared by every transport, proving they hit the same state.</summary>
public interface IGreetingStore
{
    /// <summary>Persists a greeting.</summary>
    /// <param name="greeting">The greeting to persist.</param>
    /// <param name="audit">The optional audit entry to persist with the greeting.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task SaveAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Persists a greeting and publishes its creation notification atomically.</summary>
    /// <param name="greeting">The greeting to persist.</param>
    /// <param name="audit">The optional audit entry to persist with the greeting.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task<GreetingResponse> SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Returns a page of persisted audit records.</summary>
    Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default);

    /// <summary>Returns a page of greetings.</summary>
    Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default);

    /// <summary>Reads a greeting by id or throws when missing.</summary>
    Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Attempts to read a greeting by id.</summary>
    Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a greeting after validating its opaque concurrency token.</summary>
    /// <param name="id">The greeting identifier.</param>
    /// <param name="message">The replacement message.</param>
    /// <param name="expectedETag">The expected current ETag.</param>
    /// <param name="audit">The optional audit entry to persist with the update.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task<GreetingResponse> UpdateAsync(Guid id, string message, string? expectedETag, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Gets the number of stored greetings.</summary>
    Task<int> CountAsync(CancellationToken ctk = default);

    /// <summary>Returns a snapshot of all stored greetings.</summary>
    Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default);
}

/// <summary>Thread-safe in-memory <see cref="IGreetingStore"/>.</summary>
public sealed class InMemoryGreetingStore : IGreetingStore
{
    private readonly ConcurrentDictionary<Guid, GreetingResponse> _items = new();
    private readonly ConcurrentDictionary<Guid, long> _versions = new();
    private readonly System.Threading.Lock _sync = new();
    private readonly IAuditStore _audits;

    /// <summary>Initializes a new in-memory store.</summary>
    /// <param name="audits">The shared audit store.</param>
    public InMemoryGreetingStore(IAuditStore audits)
    {
        _audits = audits;
    }

    /// <summary>Removes all greetings, versions, and audit records.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _items.Clear();
            _audits.Reset();
            _versions.Clear();
        }
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken ctk = default)
    {
        return Task.FromResult(_items.Count);
    }

    /// <inheritdoc />
    public async Task SaveAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(greeting);
        var version = _versions.GetOrAdd(greeting.Id, 1);
        _items[greeting.Id] = greeting with
        {
            ETag = $"0x{version:X16}",
        };
        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GreetingResponse> SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        await SaveAsync(greeting, audit, ctk).ConfigureAwait(false);
        return await GetAsync(greeting.Id, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        return _audits.ReadAsync(query, ctk);
    }

    /// <inheritdoc />
    public Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
    {
        var matching = _items.Values
            .Where(greeting => query.MessageContains is null
                || greeting.Message.Contains(query.MessageContains, StringComparison.OrdinalIgnoreCase))
            .OrderBy(greeting => greeting.Id)
            .ToArray();
        return Task.FromResult(new GreetingPage
        {
            Count = matching.Length,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = matching.Skip(query.Skip).Take(query.Limit).ToArray(),
        });
    }

    /// <inheritdoc />
    public Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default)
    {
        return _items.TryGetValue(id, out var greeting)
            ? Task.FromResult(greeting)
            : throw new EntityNotFoundException($"Greeting '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default)
    {
        _items.TryGetValue(id, out var greeting);
        return Task.FromResult(greeting);
    }

    /// <inheritdoc />
    public async Task<GreetingResponse> UpdateAsync(Guid id, string message, string? expectedETag, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        GreetingResponse updated;
        lock (_sync)
        {
            if (expectedETag is null || !_items.TryGetValue(id, out var current))
                throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The greeting ETag did not match.");
            var version = _versions.GetOrAdd(id, 1);
            var currentETag = $"0x{version:X16}";
            if (!string.Equals(expectedETag, currentETag, StringComparison.Ordinal))
                throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The greeting ETag did not match.");

            updated = current with
            {
                Message = message,
                ETag = $"0x{version + 1:X16}",
            };
            _items[id] = updated;
            _versions[id] = version + 1;
        }

        if (audit is not null)
            await _audits.WriteAsync(audit, ctk).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default)
    {
        return Task.FromResult<IReadOnlyCollection<GreetingResponse>>(_items.Values.ToArray());
    }
}
