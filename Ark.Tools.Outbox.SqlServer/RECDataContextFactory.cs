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
using System.Transactions;

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
            // versione 1
            _dbConnection = await _dbConnectionManager.GetAsync(_contextConfig.SQLConnectionString, cancellationToken);
            
            if (_dbConnection?.State != ConnectionState.Open)
            {
                if (_dbConnection?.State == ConnectionState.Closed)
                    await _dbConnection.OpenAsync(cancellationToken);
            }

            _dbTransaction = _dbConnection?.BeginTransactionAsync(isolationLevel, cancellationToken).GetAwaiter().GetResult();

            if (_dbConnection != null && _dbTransaction != null)
            {
                _recContext = new RECDataContext_Sql(_contextConfig, _dbConnection, _dbTransaction, isolationLevel);

                //recContext.ConnectionManager = _dbConnectionManager;

                //if (recContext.Connection?.State != ConnectionState.Open)
                //{
                //    if (recContext.Connection?.State == ConnectionState.Closed)
                //        await recContext.Connection.OpenAsync(cancellationToken);
                //}
                //if (recContext.Transaction == null)
                //{
                //    var conn = recContext.Connection;

                //    recContext.Transaction = conn?.BeginTransactionAsync(isolationLevel, cancellationToken).GetAwaiter().GetResult();
                //}

                return _recContext;
            }
            else
            {
                throw new InvalidOperationException("Error");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_recContext != null)
                await _recContext.DisposeAsync();
            if (_dbConnection != null)
                await _dbConnection.DisposeAsync();
            if (_dbTransaction != null)
                await _dbTransaction.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
