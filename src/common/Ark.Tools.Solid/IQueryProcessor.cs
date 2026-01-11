// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IQueryProcessor
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    TResult Execute<TResult>(IQuery<TResult> query);

    Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default);
}