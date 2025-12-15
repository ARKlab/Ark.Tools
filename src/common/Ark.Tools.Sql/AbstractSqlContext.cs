// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlContext<TTag> : ISqlContext<TTag>
    {
        private readonly DbConnection _connection;
        private DbTransaction? _transaction;
        private bool _disposed;
        private IsolationLevel _isolationLevel;
        private readonly Lock _lock = new();

        protected AbstractSqlContext(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _isolationLevel = isolationLevel;
        }

        protected AbstractSqlContext(DbTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _connection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction), "null Connection");
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

        public DbConnection Connection
        {
            get
            {
                _ensureOpened();
                return _connection;
            }
        }

        public DbTransaction Transaction
        {
            get
            {
                _ensureOpened();
                return _transaction;
            }
        }

        public virtual void Commit()
        {
            lock (_lock)
            {
                _transaction?.Commit();
                _transaction?.Dispose();
                _transaction = null;
            }
        }

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
