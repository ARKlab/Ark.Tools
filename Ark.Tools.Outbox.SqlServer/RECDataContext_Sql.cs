using REC.Service.Application.DAL;

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
    public partial class RECDataContext_Sql : AbstractSqlContextWithOutboxAsync<RECDataSql>, IRECDataContext
    {
        private readonly IRECDataContextConfig _config;
        private readonly IsolationLevel _isolation;

     
        public RECDataContext_Sql(IRECDataContextConfig config, DbConnection connection, DbTransaction transaction, IsolationLevel isolation)
            : base(connection, config)
        {
            _config = config;
            _isolation = isolation;
            Transaction = transaction;
        }

        //public async ValueTask InitializeCtx()
        //{
        //    //var dbConn = await _connectionManager.GetAsync(_config.SQLConnectionString);

        //    await Initialize(dbConn, _isolation);
        //}

        //public async ValueTask<IRECDataContext> CreateAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
        //{
        //    var connection = await _dbConnectionManager.GetAsync(_contextConfig.SQLConnectionString, cancellationToken);

        //    var recContext = new RECDataContext_Sql(_contextConfig, connection, isolationLevel);

        //    if (recContext.Connection?.State != ConnectionState.Open)
        //    {
        //        if (recContext.Connection?.State == ConnectionState.Closed)
        //            await recContext.Connection.OpenAsync(cancellationToken);
        //    }
        //    if (recContext.Transaction == null)
        //        recContext.Transaction = await recContext.Connection?.BeginTransactionAsync(isolationLevel, cancellationToken);

        //    return recContext;
        //}

        //public void Commit()
        //{
        //    throw new NotImplementedException();
        //}

        public async void Dispose()
        {
            if (Transaction != null)
            {
                await Transaction.DisposeAsync();
            }
            //GC.SuppressFinalize(this);
        }
    }
}
