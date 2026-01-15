// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.ResourceWatcher.WorkerHost;

/// <summary>
/// Processes a resource with type-safe extension data.
/// </summary>
/// <typeparam name="TResource">The resource type</typeparam>
/// <typeparam name="TMetadata">The resource metadata type</typeparam>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IResourceProcessor<TResource, TMetadata, TExtensions>
    where TResource : class, IResource<TMetadata, TExtensions>
    where TMetadata : class, IResourceMetadata<TExtensions>
    where TExtensions : class
{
    /// <summary>
    /// Processes the specified resource.
    /// </summary>
    /// <param name="file">The resource to process</param>
    /// <param name="ctk">Cancellation token</param>
    Task Process(TResource file, CancellationToken ctk = default);
}

/// <summary>
/// Non-generic proxy interface for backward compatibility.
/// Inherits from <see cref="IResourceProcessor{TResource, TMetadata, TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
/// <typeparam name="TResource">The resource type</typeparam>
/// <typeparam name="TMetadata">The resource metadata type</typeparam>
public interface IResourceProcessor<TResource, TMetadata> : IResourceProcessor<TResource, TMetadata, VoidExtensions>
    where TResource : class, IResource<TMetadata>
    where TMetadata : class, IResourceMetadata
{
}