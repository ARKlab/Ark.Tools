// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlContextAsync<Tag> : ISqlContextAsync<Tag>
    {
        private DbConnection _connection;
        private DbTransaction? _transaction;
        private bool _disposed = false;
        private IsolationLevel _isolationLevel;
        private object _lock = new object();

        protected AbstractSqlContextAsync(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _isolationLevel = isolationLevel;
        }

        protected AbstractSqlContextAsync(DbTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _connection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction.Connection));
            _isolationLevel = _transaction.IsolationLevel;
        }

        [MemberNotNull(nameof(_transaction))]
        private void _ensureOpened()
        {
            // we consider this double check "safe" given if someone tries to get a CONNECTION during a COMMIT ... well he should be kicked
            // this is an helper class and this is just to ensure there is always a transaction active when using the Connection
            // and to restart the Transaction automatically after a commit ONLY if Connection is reused

            lock (_lock)
            {
                if (_connection.State != ConnectionState.Open)
                {
                    if (_connection.State == ConnectionState.Closed)
                        _connection.Open();
                }
                if (_transaction == null)
                    _transaction = _connection.BeginTransaction(_isolationLevel);
            }
        }

        Task<DbConnection> ISqlContextAsync<Tag>.ConnectionAsync(CancellationToken ctk)
        {
            _ensureOpened();

            return new Task<DbConnection>(() => { return _connection; });
        }

        public Task<DbTransaction> TransactionAsync(CancellationToken ctk = default)
        {
            _ensureOpened();

            return new Task<DbTransaction>(() => {  return _transaction; });
        }

        public void ConnectionAsync(DbConnection dbConnection)
        {
            //not sure if we should just assign and then execute ensureOpen :/ 
            lock (_lock)
            {
                if (dbConnection.State != ConnectionState.Open)
                {
                    if (dbConnection.State == ConnectionState.Closed)
                        dbConnection.OpenAsync();
                }

                _connection = dbConnection;
            }
        }

        public void TransactionAsync(DbTransaction transaction)
        {
            //not sure if we should just assign and then execute ensureOpen :/ 
            lock (_lock)
            {
                if (transaction == null)
                    transaction = _connection.BeginTransaction(_isolationLevel);

                _transaction = transaction;
            }
        }

        public Task CommitAsync() => new Task(() => { Commit(); });

        public virtual void Commit()
        {
            lock (_lock)
            {
                _transaction?.Commit();
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        Task ISqlContextAsync<Tag>.RollbackAsync() => new Task(() => { Rollback(); });

        public virtual void Rollback()
        {
            lock (_lock)
            {
                _transaction?.Rollback();
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        public virtual void ChangeIsolationLevel(IsolationLevel isolationLevel)
        {
            _isolationLevel = isolationLevel;
            Rollback();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            
            if (disposing)
            {
                _transaction?.Dispose();
                _connection.Dispose();
            }

            _disposed = true;
        }

    }
}
