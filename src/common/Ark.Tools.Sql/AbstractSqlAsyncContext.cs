// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlAsyncContext<TTag> : ISqlAsyncContext<TTag>, IDisposable
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private IsolationLevel _isolationLevel;

        protected AbstractSqlAsyncContext(DbTransaction transaction)
        {
            _transaction = transaction;
            _connection = transaction.Connection!;
            _isolationLevel = transaction.IsolationLevel;
        }

        public DbConnection Connection
        {
            get
            {
                return _connection ?? throw new InvalidOperationException("There's no connection");
            }
        }

        public DbTransaction Transaction
        {
            get
            {
                return _transaction ?? throw new InvalidOperationException("There's no transaction. Use CommitAsync(true,ctk) if you want to reuse a Context. Prefer to use a new instance.");
            }
        }

        public Task CommitAsync(CancellationToken ctk = default)
        {
            return CommitAsync(false, ctk);
        }

        public virtual async Task CommitAsync(bool reuse, CancellationToken ctk = default)
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync(ctk).ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);
            }
            _transaction = null;

            if (reuse)
            {
                _transaction = await _connection!.BeginTransactionAsync(_isolationLevel, ctk).ConfigureAwait(false);
            }
        }

        public virtual async Task ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk = default)
        {
            _isolationLevel = isolationLevel;
            await RollbackAsync(ctk).ConfigureAwait(false);
        }

        public virtual async Task RollbackAsync(CancellationToken ctk = default)
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(ctk).ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);
            }

            _transaction = null;
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_transaction is not null)
                {
                    _transaction.Dispose();
                    _transaction = null;
                }

                if (_connection is not null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync().ConfigureAwait(false);
            }
            _transaction = null;

            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            _connection = null;
        }

    }
}