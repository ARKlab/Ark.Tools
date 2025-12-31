// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.ResourceWatcher.WorkerHost;

namespace Ark.Tools.ResourceWatcher.Testing;

/// <summary>
/// Stub resource provider that returns preconfigured resources.
/// Allows control over what resources are returned and simulating failures.
/// </summary>
public sealed class StubResourceProvider : IResourceProvider<StubResourceMetadata, StubResource, StubQueryFilter>
{
    private readonly List<StubResourceMetadata> _metadata = [];
    private readonly Dictionary<string, StubResource> _resources = [];
    private readonly HashSet<string> _failOnFetch = [];
    private readonly Dictionary<string, string> _returnNullOnChecksum = [];
    private Func<Exception>? _listException;

    /// <summary>
    /// Gets the number of times GetMetadata was called.
    /// </summary>
    public int ListCallCount { get; private set; }

    /// <summary>
    /// Gets the number of times GetResource was called.
    /// </summary>
    public int FetchCallCount { get; private set; }

    /// <summary>
    /// Gets the list of resource IDs that were fetched.
    /// </summary>
    public List<string> FetchedResourceIds { get; } = [];

    /// <summary>
    /// Configures the provider with metadata to be returned by GetMetadata.
    /// </summary>
    /// <param name="metadata">The metadata to return.</param>
    public void SetMetadata(IEnumerable<StubResourceMetadata> metadata)
    {
        _metadata.Clear();
        _metadata.AddRange(metadata);
    }

    /// <summary>
    /// Configures the provider with a resource to be returned by GetResource.
    /// </summary>
    /// <param name="resource">The resource to return.</param>
    public void SetResource(StubResource resource)
    {
        _resources[resource.Metadata.ResourceId] = resource;
    }

    /// <summary>
    /// Configures multiple resources at once.
    /// </summary>
    /// <param name="resources">The resources to configure.</param>
    public void SetResources(IEnumerable<StubResource> resources)
    {
        foreach (var resource in resources)
        {
            SetResource(resource);
        }
    }

    /// <summary>
    /// Configures a resource ID to fail on fetch.
    /// </summary>
    /// <param name="resourceId">The resource ID that should fail.</param>
    public void FailOnFetch(string resourceId)
    {
        _failOnFetch.Add(resourceId);
    }

    /// <summary>
    /// Configures a resource ID to return null when checksum matches (simulating no change).
    /// </summary>
    /// <param name="resourceId">The resource ID.</param>
    /// <param name="checksum">The checksum to match.</param>
    public void ReturnNullOnChecksum(string resourceId, string checksum)
    {
        _returnNullOnChecksum[resourceId] = checksum;
    }

    /// <summary>
    /// Configures the provider to throw an exception on list.
    /// </summary>
    /// <param name="exceptionFactory">A function that creates the exception to throw.</param>
    public void FailOnList(Func<Exception> exceptionFactory)
    {
        _listException = exceptionFactory;
    }

    /// <summary>
    /// Resets all configuration and counters.
    /// </summary>
    public void Reset()
    {
        _metadata.Clear();
        _resources.Clear();
        _failOnFetch.Clear();
        _returnNullOnChecksum.Clear();
        _listException = null;
        ListCallCount = 0;
        FetchCallCount = 0;
        FetchedResourceIds.Clear();
    }

    /// <inheritdoc/>
    public Task<IEnumerable<StubResourceMetadata>> GetMetadata(StubQueryFilter filter, CancellationToken ctk = default)
    {
        ListCallCount++;

        if (_listException != null)
        {
            throw _listException();
        }

        IEnumerable<StubResourceMetadata> result = _metadata;

        if (!string.IsNullOrEmpty(filter.ResourceIdPattern))
        {
            result = result.Where(m => m.ResourceId.Contains(filter.ResourceIdPattern, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<StubResource?> GetResource(StubResourceMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
    {
        FetchCallCount++;
        FetchedResourceIds.Add(metadata.ResourceId);

        if (_failOnFetch.Contains(metadata.ResourceId))
        {
            throw new InvalidOperationException($"Simulated failure for resource: {metadata.ResourceId}");
        }

        // Simulate checksum-based skipping
        if (_returnNullOnChecksum.TryGetValue(metadata.ResourceId, out var checksum) &&
            lastState?.CheckSum == checksum)
        {
            return Task.FromResult<StubResource?>(null);
        }

        if (_resources.TryGetValue(metadata.ResourceId, out var resource))
        {
            return Task.FromResult<StubResource?>(resource);
        }

        throw new KeyNotFoundException($"Resource not found: {metadata.ResourceId}");
    }
}
