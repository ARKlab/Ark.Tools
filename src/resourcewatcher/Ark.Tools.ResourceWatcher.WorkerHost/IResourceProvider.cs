// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.WorkerHost;

/// <summary>
/// Listing and Retrive data from a data provider
/// </summary>
/// <typeparam name="TMetadata">The file's metadata retrieved during </typeparam>
/// <typeparam name="TResource"></typeparam>
/// <typeparam name="TQueryFilter"></typeparam>
public interface IResourceProvider<TMetadata, TResource, TQueryFilter>
    where TMetadata : class, IResourceMetadata
    where TResource : class, IResource<TMetadata>
    where TQueryFilter : class, new()
{
    Task<IEnumerable<TMetadata>> GetMetadata(TQueryFilter filter, CancellationToken ctk = default);
    Task<TResource?> GetResource(TMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default);
}