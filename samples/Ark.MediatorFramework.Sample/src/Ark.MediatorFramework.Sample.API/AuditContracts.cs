// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using MessagePack;

using NodaTime;

using ProtoBuf;

using System.ComponentModel;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Persisted generic audit record returned by the audit query.</summary>
[ProtoContract]
[MessagePackObject]
public sealed record AuditRecord
{
    /// <summary>Gets the audit identifier.</summary>
    [ProtoMember(1)]
    [Key(0)]
    public required Guid Id { get; init; }

    /// <summary>Gets the authenticated user identifier.</summary>
    [DefaultValue("anonymous")]
    [ProtoMember(2)]
    [Key(1)]
    public string UserId { get; set; } = "anonymous";

    /// <summary>Gets the type of entity affected by the operation.</summary>
    [ProtoMember(3)]
    [Key(2)]
    public required string EntityType { get; init; }

    /// <summary>Gets the identifier of the affected entity.</summary>
    [ProtoMember(4)]
    [Key(3)]
    public required string Identifier { get; init; }

    /// <summary>Gets the operation performed on the entity.</summary>
    [ProtoMember(5)]
    [Key(4)]
    public required string Operation { get; init; }

    /// <summary>Gets the operation timestamp.</summary>
    [ProtoMember(6)]
    [Key(5)]
    public required Instant Timestamp { get; init; }
}

/// <summary>Queries the persisted audit trail.</summary>
public static class GetAuditsQuery
{
    /// <summary>Version one of the audit query.</summary>
    [HttpEndpoint("GET", "/api/v{version}/audits")]
    public sealed record V1 : IQuery<V1, PagedResult<AuditRecord>>, IQueryPaged
    {
        /// <summary>Gets the user identifier filter.</summary>
        [HttpQuery]
        public string? UserId { get; init; }

        /// <summary>Gets the entity type filter.</summary>
        [HttpQuery]
        public string? EntityType { get; init; }

        /// <summary>Gets the entity identifier filter.</summary>
        [HttpQuery]
        public string? Identifier { get; init; }

        /// <summary>Gets the inclusive lower timestamp filter.</summary>
        [HttpQuery]
        public Instant? FromTimestamp { get; init; }

        /// <summary>Gets the inclusive upper timestamp filter.</summary>
        [HttpQuery]
        public Instant? ToTimestamp { get; init; }

        /// <inheritdoc />
        [HttpQuery]
        public int Skip { get; set; }

        /// <inheritdoc />
        [HttpQuery]
        public int Limit { get; init; } = 25;

        /// <inheritdoc />
        [HttpQuery]
        public IEnumerable<string> Sort { get; init; } = [];
    }
}
