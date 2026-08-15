// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IRequestProcessor
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    TResponse Execute<TResponse>(IRequest<TResponse> request);

    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default);

    /// <summary>
    /// Executes a request implementing <see cref="IRequest{TSelf, TResponse}"/> resolving the handler
    /// at compile time, without reflection or runtime caches.
    /// </summary>
    /// <typeparam name="TRequest">The concrete request type.</typeparam>
    /// <typeparam name="TResponse">The request response type.</typeparam>
    /// <param name="request">The request to execute.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The request response.</returns>
    Task<TResponse> ExecuteAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request, CancellationToken ctk = default)
        where TRequest : class, IRequest<TRequest, TResponse>;
}