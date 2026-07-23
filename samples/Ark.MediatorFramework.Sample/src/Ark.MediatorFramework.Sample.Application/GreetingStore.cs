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
    Task SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default);

    /// <summary>Returns a page of persisted audit records.</summary>
    Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default);

    /// <summary>Reads a greeting by id or throws when missing.</summary>
    Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Attempts to read a greeting by id.</summary>
    Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Gets the number of stored greetings.</summary>
    Task<int> CountAsync(CancellationToken ctk = default);

    /// <summary>Returns a snapshot of all stored greetings.</summary>
    Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default);
}

/// <summary>Thread-safe in-memory <see cref="IGreetingStore"/>.</summary>
public sealed class InMemoryGreetingStore : IGreetingStore
{
    private readonly ConcurrentDictionary<Guid, GreetingResponse> _items = new();
    private readonly ConcurrentQueue<AuditRecord> _audits = new();

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken ctk = default)
    {
        return Task.FromResult(_items.Count);
    }

    /// <inheritdoc />
    public Task SaveAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(greeting);
        _items[greeting.Id] = greeting;
        AddAudit(audit);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        return SaveAsync(greeting, audit, ctk);
    }

    /// <inheritdoc />
    public Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        ValidateAuditSorts(query.Sort ?? []);
        var filtered = _audits.Where(record =>
            (query.UserId is null || record.UserId == query.UserId)
            && (query.EntityType is null || record.EntityType == query.EntityType)
            && (query.Identifier is null || record.Identifier == query.Identifier)
            && (query.FromTimestamp is null || record.Timestamp >= query.FromTimestamp)
            && (query.ToTimestamp is null || record.Timestamp <= query.ToTimestamp));
        var sorts = query.Sort ?? [];
        var sorted = sorts.Any()
            ? filtered.OrderBy(string.Join(", ", sorts))
            : filtered.OrderByDescending(record => record.Timestamp);
        var filteredRecords = sorted.ToArray();
        var records = filteredRecords
            .Skip(query.Skip)
            .Take(query.Limit)
            .ToArray();
        return Task.FromResult(new PagedResult<AuditRecord>
        {
            Count = filteredRecords.Length,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = records,
        });
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
            if (parts.Length == 2 && !parts[1].Equals("ASC", StringComparison.OrdinalIgnoreCase)
                && !parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid audit sort direction '{parts[1]}'.", nameof(sorts));
        }
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
    public Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default)
    {
        return Task.FromResult<IReadOnlyCollection<GreetingResponse>>(_items.Values.ToArray());
    }

    private void AddAudit(AuditEntry? audit)
    {
        if (audit is null)
            return;

        _audits.Enqueue(new AuditRecord
        {
            Id = Guid.NewGuid(),
            UserId = audit.UserId,
            EntityType = audit.EntityType,
            Identifier = audit.Identifier,
            Operation = audit.Operation,
            Timestamp = audit.Timestamp,
        });
    }
}
