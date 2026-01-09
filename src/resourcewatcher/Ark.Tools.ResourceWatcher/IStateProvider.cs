// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.ResourceWatcher;

public interface IStateProvider
{
    Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default);
}