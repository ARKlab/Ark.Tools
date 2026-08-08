// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Core.Reflection;

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Stores scenario-local audit records for in-memory application compositions.</summary>
public interface IAuditStore
{
    /// <summary>Writes an audit entry.</summary>
    /// <param name="audit">The audit entry to persist.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task WriteAsync(AuditEntry audit, CancellationToken ctk = default);

    /// <summary>Reads a page of audit records.</summary>
    /// <param name="query">The audit query.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The matching audit records.</returns>
    Task<PagedResult<AuditRecord>> ReadAsync(GetAuditsQuery query, CancellationToken ctk = default);

    /// <summary>Removes all stored audit records.</summary>
    void Reset();
}

/// <summary>Thread-safe in-memory <see cref="IAuditStore"/>.</summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    /// <inheritdoc />
    public async Task WriteAsync(AuditEntry audit, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        await Task.CompletedTask.ConfigureAwait(false);
        _records.Enqueue(new AuditRecord
        {
            Id = audit.Id,
            UserId = audit.UserId,
            EntityType = audit.EntityType,
            Identifier = audit.Identifier,
            Operation = audit.Operation,
            Timestamp = audit.Timestamp,
        });
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ReadAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateSorts(query.Sort ?? []);
        await Task.CompletedTask.ConfigureAwait(false);

        var filtered = _records.Where(record =>
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
        return new PagedResult<AuditRecord>
        {
            Count = records.LongLength,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = records.Skip(query.Skip).Take(query.Limit).ToArray(),
        };
    }

    /// <inheritdoc />
    public void Reset()
    {
        _records.Clear();
    }

    private static void ValidateSorts(IEnumerable<string> sorts)
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
}
