// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Transport-agnostic ambient user context seeded by each transport adapter
/// (Minimal API, gRPC, Rebus) before the pure handler runs.
/// </summary>
public interface IUserContext
{
    /// <summary>Gets the authenticated user identifier, or <see langword="null"/> when anonymous.</summary>
    string? UserId { get; }

    /// <summary>Gets the tenant the request operates on, or <see langword="null"/> when not scoped.</summary>
    string? Tenant { get; }
}
