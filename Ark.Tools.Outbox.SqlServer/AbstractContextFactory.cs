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
    public abstract class AbstractContextFactory<T, K, Z> : IContextFactory<T, K>
    {
        //DbConnection? _connection;
        //DbTransaction? _transaction;
        IDbConnectionManager _connectionManager;

        public AbstractContextFactory(IDbConnectionManager dbConnectionManager)
        {
            _connectionManager = dbConnectionManager;
        }

        public async ValueTask<K> CreateAsync(ISQLConnectionString connectionString, System.Data.IsolationLevel isolationLevel, CancellationToken ctk)
        {
            var connection = await _connectionManager.GetAsync(connectionString.SQLConnectionString, ctk);

            AbstractSqlContextAsync<Z>? recContext = Activator.CreateInstance(typeof(K), new object[] { connectionString, connection, isolationLevel }) as AbstractSqlContextAsync<Z>;

            if(recContext != null)
                recContext.ConnectionManager = _connectionManager;

            if (recContext?.Connection?.State != ConnectionState.Open)
            {
                if (recContext?.Connection?.State == ConnectionState.Closed)
                    await recContext.Connection.OpenAsync(ctk);
            }
            if (recContext?.Transaction == null)
            {
                var conn = recContext?.Connection;

                recContext.Transaction = conn?.BeginTransactionAsync(isolationLevel, ctk).GetAwaiter().GetResult();
            }

            return recContext;

            //if (_connection?.State != ConnectionState.Open)
            //{
            //    if (_connection?.State == ConnectionState.Closed)
            //        await _connection.OpenAsync(ctk);
            //}
            //if (_transaction == null)
            //{
            //    var conn = _connection;

            //    _transaction = conn?.BeginTransactionAsync(isolationLevel, ctk).GetAwaiter().GetResult();
            //}
        }

        public ValueTask<T> CreateAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
