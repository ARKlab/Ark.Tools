// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Concurrent;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// In-memory state provider with type-safe extension data.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public class InMemStateProvider<TExtensions> : IStateProvider<TExtensions>
{
    private readonly ConcurrentDictionary<string, ResourceState<TExtensions>> _store = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<IEnumerable<ResourceState<TExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default)
    {
        var res = new List<ResourceState<TExtensions>>();
        if (resourceIds == null)
            res.AddRange(_store.Values);
        else
        {
            foreach (var r in resourceIds)
                if (_store.TryGetValue(r, out var s))
                    res.Add(s);
        }

        return Task.FromResult(res.AsEnumerable());
    }

    /// <inheritdoc/>
    public Task SaveStateAsync(IEnumerable<ResourceState<TExtensions>> states, CancellationToken ctk = default)
    {
        foreach (var s in states)
            _store.AddOrUpdate(s.ResourceId, s, (k, v) => s);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Non-generic in-memory state provider for backward compatibility.
/// Uses <see cref="VoidExtensions"/> for extension data.
/// </summary>
public class InMemStateProvider : InMemStateProvider<VoidExtensions>
{
}