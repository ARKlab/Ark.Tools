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
    public class RECDataContextFactory : IContextFactory<IRECDataContext>, IAsyncDisposable
    {
        private readonly IRECDataContextConfig _contextConfig;
        private readonly IDbConnectionManager _dbConnectionManager;
        private DbConnection? _dbConnection;
        private DbTransaction? _dbTransaction;
        private RECDataContext_Sql? _recContext;

        public RECDataContextFactory(IRECDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager)
        {
            _contextConfig = contextConfig;
            _dbConnectionManager = dbConnectionManager;
        }

        public async ValueTask<IRECDataContext> CreateAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
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

                _recContext = new RECDataContext_Sql(_contextConfig, _dbConnection, _dbTransaction, isolationLevel);

                if (_dbTransaction != null)
                    return _recContext;
            }

            throw new InvalidOperationException("Error");

#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif

        }

        public async ValueTask DisposeAsync()
        {
            if ( _recContext != null )
                await _recContext.DisposeAsync();

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
