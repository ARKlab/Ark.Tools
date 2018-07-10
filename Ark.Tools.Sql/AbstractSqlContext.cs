// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlContext<Tag> : ISqlContext<Tag>
    {
        private readonly IDbConnection _connection;
        private IDbTransaction _transaction;
        private bool _disposed = false;
        private IsolationLevel _isolationLevel;
        protected AbstractSqlContext(IDbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");
            _connection = connection;
            if(_connection.State == ConnectionState.Closed)
                _connection.Open();
            _isolationLevel = isolationLevel;
            _transaction = _connection.BeginTransaction(_isolationLevel);
        }

        public IDbConnection Connection
        {
            get
            {
                return _connection;
            }
        }

        public IDbTransaction Transaction
        {
            get
            {
                return _transaction;
            }
        }

        public void Commit()
        {
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction(_isolationLevel);
        }

        public void Rollback()
        {
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction(_isolationLevel);
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
                _transaction.Dispose();
                _connection.Dispose();
            }

            _disposed = true;
        }
    }
}
