// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IQuery<TResult>
{
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.")]
    TResult Execute(TQuery query);

    Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default);
}