using Ark.Tools.Core;
using Ark.Tools.Sql;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public abstract class SqlContextAsyncFactory<T> : IContextFactory<T> where T : ISqlContextAsync, IAsyncDisposable
    {
        private readonly IDataContextConfig _contextConfig;
        private readonly IDbConnectionManager _dbConnectionManager;
        private IsolationLevel _isolationLevel;
        private DbConnection? _dbConnection;
        private DbTransaction? _dbTransaction;

        public SqlContextAsyncFactory(IDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager, IsolationLevel isolationLevel)
        {
            _contextConfig = contextConfig;
            _dbConnectionManager = dbConnectionManager;
            _isolationLevel = isolationLevel;
        }

        public async ValueTask<T> CreateAsync(CancellationToken cancellationToken)
        {
            _dbConnection = await _dbConnectionManager.GetAsync(_contextConfig.SqlConnectionString, cancellationToken);

            if (_dbConnection?.State != ConnectionState.Open)
            {
                if (_dbConnection?.State == ConnectionState.Closed)
                    await _dbConnection.OpenAsync(cancellationToken);
            }

#if !(NET472 || NETSTANDARD2_0)
            if (_dbConnection != null)
            {
                _dbTransaction = await _dbConnection.BeginTransactionAsync(cancellationToken);

                if (_dbTransaction != null)
                    return Create(_dbConnection, _dbTransaction);
            }

            throw new ArgumentException("Missing transaction");
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif
        }

        public abstract T Create(DbConnection dbConnection, DbTransaction dbTransaction);

        public async ValueTask DisposeAsync()
        {
#if !(NET472 || NETSTANDARD2_0)
            if ( _dbTransaction != null )
                await _dbTransaction.DisposeAsync();
            if ( _dbConnection != null )
                await _dbConnection.DisposeAsync();
#else
            if ( _dbTransaction != null )
                _dbTransaction.Dispose();
            if ( _dbConnection != null )
                _dbConnection.Dispose();
#endif
        }
    }
}
