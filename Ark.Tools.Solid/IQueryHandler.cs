// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface IQuery<TResult>
    {
    }

    public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        TResult Execute(TQuery query);

        Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default);
    }
}
