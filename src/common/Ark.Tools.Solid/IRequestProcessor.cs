// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IRequestProcessor
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.")]
    TResponse Execute<TResponse>(IRequest<TResponse> request);

    Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default);
}