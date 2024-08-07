﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.WorkerHost
{
    /// <summary>
    /// Process a <typeparamref name="TResource"/>
    /// </summary>
    /// <typeparam name="TResource">The resource type</typeparam>
    /// <typeparam name="TMetadata">The resource metadata</typeparam>
    public interface IResourceProcessor<TResource, TMetadata>
        where TResource : class, IResource<TMetadata>
        where TMetadata : class, IResourceMetadata
    {
        Task Process(TResource file, CancellationToken ctk = default);
    }
}
