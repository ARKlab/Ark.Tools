using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Ark.Tools.Sql
{
    public class AbstractSqlContextAsync<Tag> : ISqlContextAsync<Tag>
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        //private IsolationLevel _isolationLevel;
        private IDbConnectionManager? _connectionManager;

        public IDbConnectionManager? ConnectionManager { get { return _connectionManager; } set { _connectionManager = value; } }

        public AbstractSqlContextAsync(/*IsolationLevel isolationLevel = IsolationLevel.ReadCommitted*/)
        {
            //_connection = connection ?? throw new ArgumentNullException(nameof(connection));
            //_isolationLevel = isolationLevel;

            //Create(isolationLevel,)
        }

        public AbstractSqlContextAsync(DbConnection connection, DbTransaction transaction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            //_isolationLevel = isolationLevel;

            //Create(isolationLevel,)
        }

        public AbstractSqlContextAsync(DbTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _connection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction.Connection));
            //_isolationLevel = _transaction.IsolationLevel;
        }

        //public async ValueTask CreateConnectionManager(string connectionString)
        //{
        //    if(_connectionManager != null)
        //        _connection = await _connectionManager.GetAsync(connectionString);
        //}

        //public async ValueTask Initialize(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ctk = default)
        //{
        //    await Create(isolationLevel, ctk);
        //}

        public DbConnection Connection
        {
            get
            {
                if (_connection == null)
                    throw new InvalidOperationException("Not valid Operation");
                return _connection;
            }
        }

        public DbTransaction Transaction
        {
            get
            {
                if (_transaction == null)
                    throw new InvalidOperationException("Not valid Operation");
                return _transaction;
            }
            set
            {
                _transaction = value;
            }
        }

        public virtual async ValueTask CommitAsync(CancellationToken ctk)
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync(ctk);
                await _transaction.DisposeAsync();
            }
            _transaction = null;
        }

        public virtual async ValueTask CommitAndRestartTransactionAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync(ctk);
                await _transaction.DisposeAsync();
            }
            _transaction = null;
            await _createAsync(isolationLevel, ctk);
        }

        public async ValueTask RollbackAsync(CancellationToken ctk)
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(ctk);
                await _transaction.DisposeAsync();
            }
            
            _transaction = null;
        }

        private async ValueTask _createAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
            _connection = await _connectionManager?.GetAsync(_config.SQLConnectionString);

            //_isolationLevel = isolationLevel;

            if (_connection?.State != ConnectionState.Open)
            {
                if (_connection?.State == ConnectionState.Closed)
                    await _connection.OpenAsync(ctk);
            }
            if (_transaction == null)
                _transaction = await _connection?.BeginTransactionAsync(isolationLevel, ctk);
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }
    }
}
