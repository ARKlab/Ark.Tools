// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.Collections.Concurrent;

namespace Ark.Tools.ResourceWatcher.Testing;

/// <summary>
/// A testable state provider that extends InMemStateProvider with tenant filtering,
/// assertion helpers, and the ability to pre-populate and inspect state for testing.
/// </summary>
public class TestableStateProvider : IStateProvider
{
    private readonly ConcurrentDictionary<(string Tenant, string ResourceId), ResourceState<VoidExtensions>> _store = new();

    /// <summary>
    /// Loads state for the given tenant and optional resource IDs.
    /// </summary>
    public Task<IEnumerable<ResourceState<VoidExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default)
    {
        IEnumerable<ResourceState<VoidExtensions>> res;

        if (resourceIds == null)
        {
            res = _store.Values.Where(s => string.Equals(s.Tenant, tenant, StringComparison.Ordinal));
        }
        else
        {
            res = resourceIds
                .Where(r => _store.ContainsKey((tenant, r)))
                .Select(r => _store[(tenant, r)]);
        }

        return Task.FromResult(res.AsEnumerable());
    }

    /// <summary>
    /// Saves state for the given resources.
    /// </summary>
    public Task SaveStateAsync(IEnumerable<ResourceState<VoidExtensions>> states, CancellationToken ctk = default)
    {
        foreach (var s in states)
        {
            var key = (s.Tenant ?? string.Empty, s.ResourceId);
            _store.AddOrUpdate(key, s, (k, v) => s);
        }

        return Task.CompletedTask;
    }

    #region Test Helpers

    /// <summary>
    /// Gets the state for a specific resource, or null if not found.
    /// </summary>
    public ResourceState<VoidExtensions>? GetState(string tenant, string resourceId)
    {
        _store.TryGetValue((tenant, resourceId), out var state);
        return state;
    }

    /// <summary>
    /// Gets the state for a resource in the default tenant.
    /// This is a convenience method for single-tenant testing scenarios.
    /// </summary>
    public ResourceState<VoidExtensions>? GetResourceState(string tenant, string resourceId) => GetState(tenant, resourceId);

    /// <summary>
    /// Sets state for a resource. Convenience method for testing.
    /// </summary>
    public void SetResourceState(string tenant, ResourceState<VoidExtensions> state)
    {
        var stateWithTenant = new ResourceState<VoidExtensions>
        {
            Tenant = tenant,
            ResourceId = state.ResourceId,
            Modified = state.Modified,
            ModifiedSources = state.ModifiedSources,
            CheckSum = state.CheckSum,
            RetryCount = state.RetryCount,
            LastEvent = state.LastEvent,
            RetrievedAt = state.RetrievedAt
        };
        SetState(stateWithTenant);
    }

    /// <summary>
    /// Gets all states for a tenant.
    /// </summary>
    public IReadOnlyList<ResourceState<VoidExtensions>> GetAllStates(string tenant)
    {
        return _store.Values.Where(s => string.Equals(s.Tenant, tenant, StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// Sets state directly for testing purposes.
    /// </summary>
    public void SetState(ResourceState<VoidExtensions> state)
    {
        var key = (state.Tenant ?? string.Empty, state.ResourceId);
        _store.AddOrUpdate(key, state, (k, v) => state);
    }

    /// <summary>
    /// Sets state directly for testing purposes using individual parameters.
    /// </summary>
    public void SetState(
        string tenant,
        string resourceId,
        LocalDateTime modified,
        Dictionary<string, LocalDateTime>? modifiedSources,
        string? checkSum,
        int retryCount,
        Instant lastEvent,
        Instant? retrievedAt = null)
    {
        var state = new ResourceState<VoidExtensions>
        {
            Tenant = tenant,
            ResourceId = resourceId,
            Modified = modified,
            ModifiedSources = modifiedSources,
            CheckSum = checkSum,
            RetryCount = retryCount,
            LastEvent = lastEvent,
            RetrievedAt = retrievedAt
        };
        SetState(state);
    }

    /// <summary>
    /// Clears all state for a tenant.
    /// </summary>
    public void Clear(string tenant)
    {
        var keysToRemove = _store.Keys.Where(k => string.Equals(k.Tenant, tenant, StringComparison.Ordinal)).ToList();
        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Clears all state.
    /// </summary>
    public void ClearAll()
    {
        _store.Clear();
    }

    /// <summary>
    /// Gets the retry count for a resource.
    /// </summary>
    public int GetRetryCount(string tenant, string resourceId)
    {
        return GetState(tenant, resourceId)?.RetryCount ?? 0;
    }

    /// <summary>
    /// Checks if a resource is banned (RetryCount > maxRetries).
    /// </summary>
    public bool IsBanned(string tenant, string resourceId, uint maxRetries)
    {
        var state = GetState(tenant, resourceId);
        return state != null && state.RetryCount > maxRetries;
    }

    /// <summary>
    /// Gets the count of resources in a specific state.
    /// </summary>
    public int CountByRetryCount(string tenant, int retryCount)
    {
        return _store.Values
            .Count(s => string.Equals(s.Tenant, tenant, StringComparison.Ordinal) && s.RetryCount == retryCount);
    }

    /// <summary>
    /// Gets the count of banned resources.
    /// </summary>
    public int CountBanned(string tenant, uint maxRetries)
    {
        return _store.Values
            .Count(s => string.Equals(s.Tenant, tenant, StringComparison.Ordinal) && s.RetryCount > maxRetries);
    }

    #endregion
}