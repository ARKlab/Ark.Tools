// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

namespace Ark.MediatorFramework.Sample.Application.Services;

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
