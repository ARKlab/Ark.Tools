// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public interface IStoredProcedure<TResult> where TResult : notnull
    {
        TResult? LastResult { get; }
        TResult? Execute();
        Task<TResult?> ExecuteAsync();
    }

    public interface IStoredProcedure<TResult, TParameter> where TResult : notnull
    {
        TResult? LastResult { get; }
        TResult? Execute(TParameter param);
        Task<TResult?> ExecuteAsync(TParameter param);
    }
}