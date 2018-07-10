// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public abstract class AbstractStoredProcedure<TResult, TParameter> : IStoredProcedure<TResult, TParameter>
    {
        protected IDbConnection Connection;
        protected IDbTransaction Transaction;

        public TResult LastResult { get; protected set; }

        protected AbstractStoredProcedure(IDbConnection connection, IDbTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        public TResult Execute(TParameter param)
        {
            LastResult = ExecuteImplAsync(Connection, param, transaction: Transaction).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult> ExecuteAsync(TParameter param)
        {
            LastResult = await ExecuteImplAsync(Connection, param, transaction: Transaction).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult> ExecuteImplAsync(IDbConnection conn, TParameter param, IDbTransaction transaction);
    }

    public abstract class AbstractStoredProcedure<TResult> : IStoredProcedure<TResult>
    {
        protected IDbConnection Connection;
        protected IDbTransaction Transaction;

        public TResult LastResult { get; protected set; }

        protected AbstractStoredProcedure(IDbConnection connection, IDbTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }
        public TResult Execute()
        {
            LastResult = ExecuteImplAsync(Connection, transaction: Transaction).GetAwaiter().GetResult();
            return LastResult;
        }

        public async Task<TResult> ExecuteAsync()
        {
            LastResult = await ExecuteImplAsync(Connection, transaction: Transaction).ConfigureAwait(false);
            return LastResult;
        }


        protected abstract Task<TResult> ExecuteImplAsync(IDbConnection conn, IDbTransaction transaction);
    }
}