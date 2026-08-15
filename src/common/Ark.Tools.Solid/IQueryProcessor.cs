// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IQueryProcessor
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    TResult Execute<TResult>(IQuery<TResult> query);

    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default);

    /// <summary>
    /// Executes a query implementing <see cref="IQuery{TSelf, TResult}"/> resolving the handler
    /// at compile time, without reflection or runtime caches.
    /// </summary>
    /// <typeparam name="TQuery">The concrete query type.</typeparam>
    /// <typeparam name="TResult">The query result type.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The query result.</returns>
    Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
        where TQuery : class, IQuery<TQuery, TResult>;
}