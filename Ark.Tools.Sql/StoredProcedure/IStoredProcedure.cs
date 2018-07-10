// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public interface IStoredProcedure<TResult>
    {
        TResult LastResult { get; }
        TResult Execute();
        Task<TResult> ExecuteAsync();
    }

    public interface IStoredProcedure<TResult, TParameter>
    {
        TResult LastResult { get; }
        TResult Execute(TParameter param);
        Task<TResult> ExecuteAsync(TParameter param);
    }
}