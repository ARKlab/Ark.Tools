// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.ResourceWatcher.WorkerHost;

/// <summary>
/// Provides listing and retrieval of resources from a data provider with type-safe extension data.
/// </summary>
/// <typeparam name="TMetadata">The resource metadata type</typeparam>
/// <typeparam name="TResource">The resource type</typeparam>
/// <typeparam name="TQueryFilter">The query filter type</typeparam>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>
    where TMetadata : class, IResourceMetadata<TExtensions>
    where TResource : class, IResource<TMetadata, TExtensions>
    where TQueryFilter : class, new()
    where TExtensions : class
{
    /// <summary>
    /// Gets metadata for resources matching the specified filter.
    /// </summary>
    /// <param name="filter">The query filter to apply</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>Collection of resource metadata</returns>
    Task<IEnumerable<TMetadata>> GetMetadata(TQueryFilter filter, CancellationToken ctk = default);
    
    /// <summary>
    /// Gets the full resource for the specified metadata.
    /// </summary>
    /// <param name="metadata">The resource metadata</param>
    /// <param name="lastState">The last tracked state of the resource</param>
    /// <param name="ctk">Cancellation token</param>
    /// <returns>The resource, or null if not available</returns>
    Task<TResource?> GetResource(TMetadata metadata, IResourceTrackedState<TExtensions>? lastState, CancellationToken ctk = default);
}

/// <summary>
/// Non-generic proxy interface for backward compatibility.
/// Inherits from <see cref="IResourceProvider{TMetadata, TResource, TQueryFilter, TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
/// <typeparam name="TMetadata">The resource metadata type</typeparam>
/// <typeparam name="TResource">The resource type</typeparam>
/// <typeparam name="TQueryFilter">The query filter type</typeparam>
public interface IResourceProvider<TMetadata, TResource, TQueryFilter> : IResourceProvider<TMetadata, TResource, TQueryFilter, VoidExtensions>
    where TMetadata : class, IResourceMetadata
    where TResource : class, IResource<TMetadata>
    where TQueryFilter : class, new()
{
}