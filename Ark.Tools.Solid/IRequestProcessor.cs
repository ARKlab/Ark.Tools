// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface IRequestProcessor
    {
        TResponse Execute<TResponse>(IRequest<TResponse> request);

        Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default);
    }
}
