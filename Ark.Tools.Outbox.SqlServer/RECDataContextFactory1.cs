using Ark.Tools.Core;
using Ark.Tools.Sql;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Ark.Tools.Outbox.SqlServer
{
    public class RECDataContextFactory1 : AbstractContextFactory1, IContextFactory1<IRECDataContext>, IAsyncDisposable
    {
        private readonly IRECDataContextConfig _contextConfig;
        private readonly IDbConnectionManager _dbConnectionManager;

        public RECDataContextFactory1(IRECDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager) : base(dbConnectionManager)
        {
            _contextConfig = contextConfig;
            _dbConnectionManager = dbConnectionManager;
        }

        public async ValueTask<IRECDataContext> CreateAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            // versione 1
            //var connection = await _dbConnectionManager.GetAsync(_contextConfig.SQLConnectionString, cancellationToken);

            //var recContext = new RECDataContext_Sql(_contextConfig, connection, isolationLevel);

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

            //return recContext;

            await CreateConnectionAndTransactionAsync(_contextConfig, isolationLevel, cancellationToken);

            if (Connection != null && Transaction != null)
            {
                var recContext = new RECDataContext_Sql(_contextConfig, connection: Connection, transaction: Transaction, isolationLevel);

                return recContext;
            }
            else
            {
                throw new InvalidOperationException("Error");
            }
        }

        public ValueTask<IRECDataContext> CreateAsync(ISQLConnectionString connectionStrin, System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async ValueTask DisposeAsync()
        {
            if (Connection != null)
                await Connection.DisposeAsync();
            if (Transaction != null)
                await Transaction.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
