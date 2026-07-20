// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using NodaTime;
using NodaTime.Text;

using ProtoBuf;

using Microsoft.AspNetCore.Http;

using System.Reflection;

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

/// <summary>Queries the persisted audit trail.</summary>
[HttpEndpoint("GET", "/api/v{version}/audits")]
public sealed record GetAuditsQuery : IQuery<PagedResult<AuditRecord>>, IQueryPaged, IBindableFromHttpContext<GetAuditsQuery>
{
    /// <summary>Binds the audit query from the HTTP query string.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="parameter">The bound handler parameter.</param>
    /// <returns>The bound query, or <see langword="null"/> when a value is invalid.</returns>
    public static ValueTask<GetAuditsQuery?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var query = context.Request.Query;
        if (!int.TryParse(query["skip"].FirstOrDefault() ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            || !int.TryParse(query["limit"].FirstOrDefault() ?? "25", NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            || !TryParseInstant(query["fromTimestamp"].FirstOrDefault(), out var fromTimestamp)
            || !TryParseInstant(query["toTimestamp"].FirstOrDefault(), out var toTimestamp))
        {
            return ValueTask.FromResult<GetAuditsQuery?>(null);
        }

        return ValueTask.FromResult<GetAuditsQuery?>(new GetAuditsQuery
        {
            UserId = query["userId"].FirstOrDefault(),
            EntityType = query["entityType"].FirstOrDefault(),
            Identifier = query["identifier"].FirstOrDefault(),
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp,
            Skip = skip,
            Limit = limit,
            Sort = query["sort"].Where(value => value is not null).Select(value => value!).ToArray(),
        });
    }

    private static bool TryParseInstant(string? value, out Instant? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        var parsed = InstantPattern.ExtendedIso.Parse(value);
        result = parsed.Success ? parsed.Value : null;
        return parsed.Success;
    }

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
    public Instant? FromTimestamp { get; init; }

    /// <summary>Gets the inclusive upper timestamp filter.</summary>
    [BindFromQuery]
    public Instant? ToTimestamp { get; init; }

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
