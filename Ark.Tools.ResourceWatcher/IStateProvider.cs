// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    public interface IStateProvider
    {
        Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
        Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default);
    }


    public class InMemStateProvider : IStateProvider
    {
        private ConcurrentDictionary<string, ResourceState> _store = new(System.StringComparer.Ordinal);

        public Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default)
        {
            var res = new List<ResourceState>();
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

        public Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default)
        {
            foreach (var s in states)
                _store.AddOrUpdate(s.ResourceId, s, (k, v) => s);

            return Task.CompletedTask;
        }
    }
}