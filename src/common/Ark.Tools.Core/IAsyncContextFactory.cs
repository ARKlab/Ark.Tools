// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Core;

public interface IAsyncContextFactory<T> where T : IAsyncContext
{
    Task<T> CreateAsync(CancellationToken ctk = default);
}