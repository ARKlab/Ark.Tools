// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// State provider interface for managing resource state with type-safe extension data.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IStateProvider<TExtensions>
{
    /// <summary>
    /// Loads state for resources in the specified tenant.
    /// </summary>
    /// <param name="tenant">The tenant identifier</param>
    /// <param name="resourceIds">Optional array of specific resource IDs to load. If null, loads all.</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>Collection of resource states</returns>
    Task<IEnumerable<ResourceState<TExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    
    /// <summary>
    /// Saves state for the specified resources.
    /// </summary>
    /// <param name="states">The resource states to save</param>
    /// <param name="ctk">Cancellation token</param>
    Task SaveStateAsync(IEnumerable<ResourceState<TExtensions>> states, CancellationToken ctk = default);
}

/// <summary>
/// Non-generic state provider interface for backward compatibility.
/// Inherits from <see cref="IStateProvider{TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
public interface IStateProvider : IStateProvider<VoidExtensions>
{
}