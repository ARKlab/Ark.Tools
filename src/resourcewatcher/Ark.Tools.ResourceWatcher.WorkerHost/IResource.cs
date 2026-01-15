// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.ResourceWatcher.WorkerHost;

/// <summary>
/// Represents a resource, including its metadata and data (a parsed resource) with type-safe extension data.
/// </summary>
/// <typeparam name="TMetadata">The metadata class</typeparam>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IResource<TMetadata, TExtensions> : IResourceState
    where TMetadata : class, IResourceMetadata<TExtensions>
{
    /// <summary>
    /// Gets the metadata for this resource.
    /// </summary>
    TMetadata Metadata { get; }
}

/// <summary>
/// Non-generic proxy interface for backward compatibility.
/// Inherits from <see cref="IResource{TMetadata, TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
/// <typeparam name="TMetadata">The metadata class</typeparam>
public interface IResource<TMetadata> : IResource<TMetadata, VoidExtensions>
    where TMetadata : class, IResourceMetadata
{
}