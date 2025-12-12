// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data.Common;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public abstract class AbstractStoredProcedure<TResult, TParameter> : IStoredProcedure<TResult, TParameter> where TResult : notnull
    {

        public TResult? LastResult { get; protected set; }
        protected DbTransaction Transaction { get; private set; }

        protected AbstractStoredProcedure(DbTransaction transaction)
        {
            Transaction = transaction;
        }

        public TResult? Execute(TParameter param)
        {
            LastResult = ExecuteImplAsync(Transaction, param).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult?> ExecuteAsync(TParameter param)
        {
            LastResult = await ExecuteImplAsync(Transaction, param).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult?> ExecuteImplAsync(DbTransaction transaction, TParameter param);
    }

    public abstract class AbstractStoredProcedure<TResult> : IStoredProcedure<TResult> where TResult : notnull
    {
        protected DbTransaction Transaction { get; private set; }

        public TResult? LastResult { get; protected set; }

        protected AbstractStoredProcedure(DbTransaction transaction)
        {
            Transaction = transaction;
        }
        public TResult? Execute()
        {
            LastResult = ExecuteImplAsync(Transaction).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult?> ExecuteAsync()
        {
            LastResult = await ExecuteImplAsync(Transaction).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult?> ExecuteImplAsync(DbTransaction transaction);
    }
}