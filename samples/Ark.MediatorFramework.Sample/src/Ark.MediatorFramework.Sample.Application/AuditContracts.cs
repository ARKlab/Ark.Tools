// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using NodaTime;
using NodaTime.Text;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Describes an operation to be persisted in the audit trail.</summary>
public sealed record AuditEntry
{
    /// <summary>Gets the audit identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets the authenticated user identifier.</summary>
    public string UserId { get; init; } = "anonymous";

    /// <summary>Gets the type of entity affected by the operation.</summary>
    public required string EntityType { get; init; }

    /// <summary>Gets the identifier of the affected entity.</summary>
    public required string Identifier { get; init; }

    /// <summary>Gets the operation performed on the entity.</summary>
    public required string Operation { get; init; }

    /// <summary>Gets the operation timestamp.</summary>
    public required Instant Timestamp { get; init; }
}

/// <summary>Persisted generic audit record returned by the audit query.</summary>
[ProtoContract]
public sealed record AuditRecord
{
    /// <summary>Gets the audit identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the authenticated user identifier.</summary>
    [ProtoMember(2)]
    public string UserId { get; init; } = "anonymous";

    /// <summary>Gets the type of entity affected by the operation.</summary>
    [ProtoMember(3)]
    public required string EntityType { get; init; }

    /// <summary>Gets the identifier of the affected entity.</summary>
    [ProtoMember(4)]
    public required string Identifier { get; init; }

    /// <summary>Gets the operation performed on the entity.</summary>
    [ProtoMember(5)]
    public required string Operation { get; init; }

    /// <summary>Gets the operation timestamp.</summary>
    [ProtoMember(6)]
    public required Instant Timestamp { get; init; }
}

/// <summary>Represents an audit timestamp parsed from an ISO-8601 query value.</summary>
public readonly record struct AuditTimestamp(Instant Value)
{
    /// <summary>Parses an ISO-8601 instant.</summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="result">The parsed timestamp.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out AuditTimestamp result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        var parsed = InstantPattern.ExtendedIso.Parse(value);
        result = parsed.Success ? new AuditTimestamp(parsed.Value) : default;
        return parsed.Success;
    }

    /// <summary>Parses an ISO-8601 instant.</summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <param name="result">The parsed timestamp.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, IFormatProvider? formatProvider, out AuditTimestamp result)
    {
        return TryParse(value, out result);
    }
}

/// <summary>Queries the persisted audit trail.</summary>
[HttpEndpoint("GET", "/api/v{version}/audits")]
public sealed record GetAuditsQuery : IQuery<PagedResult<AuditRecord>>, IQueryPaged
{
    /// <summary>Gets the user identifier filter.</summary>
    [BindFromQuery]
    public string? UserId { get; init; }

    /// <summary>Gets the entity type filter.</summary>
    [BindFromQuery]
    public string? EntityType { get; init; }

    /// <summary>Gets the entity identifier filter.</summary>
    [BindFromQuery]
    public string? Identifier { get; init; }

    /// <summary>Gets the inclusive lower timestamp filter.</summary>
    [BindFromQuery]
    public AuditTimestamp? FromTimestamp { get; init; }

    /// <summary>Gets the inclusive upper timestamp filter.</summary>
    [BindFromQuery]
    public AuditTimestamp? ToTimestamp { get; init; }

    /// <inheritdoc />
    [BindFromQuery]
    public int Skip { get; set; }

    /// <inheritdoc />
    [BindFromQuery]
    public int Limit { get; init; } = 25;

    /// <inheritdoc />
    [BindFromQuery]
    public IEnumerable<string> Sort { get; init; } = [];
}
