// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IQuery<TResult>
{
}

/// <summary>
/// Self-referencing variant of <see cref="IQuery{TResult}"/> that enables reflection-free dispatch.
/// Declare queries as <c>class MyQuery : IQuery&lt;MyQuery, MyResult&gt;</c> so that
/// <see cref="IQueryProcessor.ExecuteAsync{TQuery, TResult}(IQuery{TQuery, TResult}, CancellationToken)"/>
/// can infer both type arguments at the call site and resolve the handler without reflection.
/// </summary>
/// <typeparam name="TSelf">The concrete query type implementing this interface.</typeparam>
/// <typeparam name="TResult">The query result type.</typeparam>
public interface IQuery<TSelf, TResult> : IQuery<TResult>
    where TSelf : IQuery<TSelf, TResult>
{
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default);
}