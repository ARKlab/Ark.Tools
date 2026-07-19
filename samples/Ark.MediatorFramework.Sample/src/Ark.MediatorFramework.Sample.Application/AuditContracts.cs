// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using NodaTime;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Describes a mutation to be persisted in the audit trail.</summary>
public sealed record AuditEntry
{
    /// <summary>Gets the authenticated user identifier.</summary>
    public string UserId { get; init; } = "anonymous";

    /// <summary>Gets the name of the mutated contract.</summary>
    public required string Contract { get; init; }

    /// <summary>Gets the mutation timestamp.</summary>
    public required Instant Timestamp { get; init; }
}

/// <summary>Persisted audit record returned by the audit query.</summary>
[ProtoContract]
public sealed record AuditRecord
{
    /// <summary>Gets the audit identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the authenticated user identifier.</summary>
    [ProtoMember(2)]
    public string UserId { get; init; } = "anonymous";

    /// <summary>Gets the mutated contract name.</summary>
    [ProtoMember(3)]
    public required string Contract { get; init; }

    /// <summary>Gets the mutation timestamp.</summary>
    [ProtoMember(4)]
    public required Instant Timestamp { get; init; }
}

/// <summary>Queries the persisted audit trail.</summary>
[HttpEndpoint("GET", "/api/v{version}/audits")]
public sealed record GetAuditsQuery : IQuery<PagedResult<AuditRecord>>, IQueryPaged
{
    /// <inheritdoc />
    public int Skip { get; set; }

    /// <inheritdoc />
    public int Limit { get; init; } = 25;

    /// <inheritdoc />
    public IEnumerable<string> Sort { get; init; } = [];
}
