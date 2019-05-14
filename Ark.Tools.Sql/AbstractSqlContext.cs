// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;
using System.Data.Common;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlContext<Tag> : ISqlContext<Tag>
    {
        private readonly DbConnection _connection;
        private DbTransaction _transaction;
        private bool _disposed = false;
        private IsolationLevel _isolationLevel;
        protected AbstractSqlContext(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");

            _connection = connection;
            _isolationLevel = isolationLevel;
            _opened = _connection.State != ConnectionState.Closed;
        }

        private volatile bool _opened = false;
        private void _ensureOpened()
        {
            // we consider this double check "safe" given if someone tries to get a CONNECTION during a COMMIT ... well he should be kicked
            // this is an helper class and this is just to ensure there is always a transaction active when using the Connection
            // and to restart the Transaction automatically after a commit ONLY if Connection is reused

            if (!_opened)
            {
                lock (_connection)
                {
                    if (!_opened)
                    {
                        if (_connection.State == ConnectionState.Closed)
                            _connection.Open();
                        _transaction = _connection.BeginTransaction(_isolationLevel);
                    }
                    _opened = true;
                }
            }

            if (_transaction == null)
            {
                lock (_connection)
                {
                    if (_transaction == null)
                        _transaction = _connection.BeginTransaction(_isolationLevel);
                }
            }
        }

        public IDbConnection Connection
        {
            get
            {
                _ensureOpened();
                return _connection;
            }
        }

        public IDbTransaction Transaction
        {
            get
            {
                _ensureOpened();
                return _transaction;
            }
        }

        public void Commit()
        {
            lock (_connection)
            {
                _transaction?.Commit();
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        public void Rollback()
        {
            lock (_connection)
            {
                _transaction?.Rollback();
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        public void ChangeIsolationLevel(IsolationLevel isolationLevel)
        {
            _isolationLevel = isolationLevel;
            Rollback();
        }

        ~AbstractSqlContext()
        {
            Dispose(false);
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
