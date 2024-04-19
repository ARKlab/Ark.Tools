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
        private IsolationLevel _isolationLevel;
        private IDbConnectionManager? _connectionManager;

        public IDbConnectionManager? ConnectionManager { get { return _connectionManager; } set { _connectionManager = value; } }

        public AbstractSqlContextAsync(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _isolationLevel = isolationLevel;
        }

        public AbstractSqlContextAsync(DbTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _connection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction.Connection));
            _isolationLevel = transaction.IsolationLevel;
        }


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

        public virtual async ValueTask CommitAysnc(CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            if (_transaction != null)
            {
                await _transaction.CommitAsync(ctk);
                await _transaction.DisposeAsync();
            }
            _transaction = null;
#endif
        }

        public virtual async ValueTask CommitAndRestartTransactionAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            if (_transaction != null)
            {
                await _transaction.CommitAsync(ctk);
                await _transaction.DisposeAsync();
            }
            _transaction = null;
            await _createAsync(isolationLevel, ctk);
#endif
        }

        public async ValueTask ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            _isolationLevel = isolationLevel;
            await RollbackAsync(ctk);
#endif
        }

        public async ValueTask RollbackAsync(CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(ctk);
                await _transaction.DisposeAsync();
            }

            _transaction = null;
#endif
        }

        private async ValueTask _createAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            if (_connection.State != ConnectionState.Open)
            {
                if (_connection.State == ConnectionState.Closed)
                    await _connection.OpenAsync(ctk);
            }
            if (_transaction == null)
                _transaction = await _connection.BeginTransactionAsync(isolationLevel, ctk);
#endif
        }

        public async ValueTask DisposeAsync()
        {
#if !(NETSTANDARD2_0 || NET472)
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
            GC.SuppressFinalize(this);
#endif
        }
    }
}
