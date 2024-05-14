using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Sql
{
    public abstract class AbstractSqlContextAsync<Tag> : ISqlContextAsync
    {
        private DbConnection? _connection;
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
                return _connection?? throw new InvalidProgramException("There's no connection");
            }
        }

        public DbTransaction Transaction
        {
            get
            {
                return _transaction?? throw new InvalidProgramException("There's no transaction");
            }
        }

        public async Task CreateAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            if (_connectionManager == null)
                throw new ConstraintException("There is no connection manager available to create the connection from");

            _connection = await _connectionManager.GetAsync("_contextConfig.SqlConnectionString", cancellationToken);

            if (Connection?.State != ConnectionState.Open)
            {
                if (Connection?.State == ConnectionState.Closed)
                    await Connection.OpenAsync(cancellationToken);
            }

#if !(NET472 || NETSTANDARD2_0)
            if (Connection != null)
            {
                _transaction = await Connection.BeginTransactionAsync(isolationLevel, cancellationToken);
            }

            throw new InvalidOperationException("Error");

#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif

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
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif
        }

        public async ValueTask ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
#if !(NETSTANDARD2_0 || NET472)
            _isolationLevel = isolationLevel;
            await RollbackAsync(ctk);
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
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
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
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
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif
        }
    }
}
