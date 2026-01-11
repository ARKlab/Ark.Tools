// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IRequest<TResponse>
{
}

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.")]
    TResponse Execute(TRequest Request);

    Task<TResponse> ExecuteAsync(TRequest Request, CancellationToken ctk = default);
}