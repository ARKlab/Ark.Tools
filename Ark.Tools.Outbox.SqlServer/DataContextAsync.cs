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
    public class DataContextAsync : IDataContextAsync, IAsyncDisposable
    {
        private readonly IDataContextConfig _contextConfig;
        private readonly IDbConnectionManager _dbConnectionManager;
        private DbConnection? _dbConnection;
        private DbTransaction? _dbTransaction;

        public DataContextAsync(IDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager)
        {
            _contextConfig = contextConfig;
            _dbConnectionManager = dbConnectionManager;
        }

        public async Task<T> CreateAsync<T>(IsolationLevel isolationLevel, CancellationToken cancellationToken)
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
                _dbTransaction = await _dbConnection.BeginTransactionAsync(isolationLevel, cancellationToken);

                var ctx = Activator.CreateInstance(typeof(T), new object[]{ _contextConfig, _dbConnection, _dbTransaction, isolationLevel });

                if (_dbTransaction != null && ctx != null)
                    return (T)ctx;
            }

            throw new InvalidOperationException("Error");

#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif

        }

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
            GC.SuppressFinalize(this);
        }
    }
}
