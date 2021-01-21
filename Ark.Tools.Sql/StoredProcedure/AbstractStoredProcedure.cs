// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public abstract class AbstractStoredProcedure<TResult, TParameter> : IStoredProcedure<TResult, TParameter>
    {
        protected DbTransaction Transaction;

        public TResult LastResult { get; protected set; }

        protected AbstractStoredProcedure(DbTransaction transaction)
        {
            Transaction = transaction;
        }

        public TResult Execute(TParameter param)
        {
            LastResult = ExecuteImplAsync(Transaction, param).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult> ExecuteAsync(TParameter param)
        {
            LastResult = await ExecuteImplAsync(Transaction, param).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult> ExecuteImplAsync(DbTransaction transaction, TParameter param);
    }

    public abstract class AbstractStoredProcedure<TResult> : IStoredProcedure<TResult>
    {
        protected DbTransaction Transaction;

        public TResult LastResult { get; protected set; }

        protected AbstractStoredProcedure(DbTransaction transaction)
        {
            Transaction = transaction;
        }
        public TResult Execute()
        {
            LastResult = ExecuteImplAsync(Transaction).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult> ExecuteAsync()
        {
            LastResult = await ExecuteImplAsync(Transaction).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult> ExecuteImplAsync(DbTransaction transaction);
    }
}