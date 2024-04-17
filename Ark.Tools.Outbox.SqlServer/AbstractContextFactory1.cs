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
    public abstract class AbstractContextFactory1
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private IDbConnectionManager _connectionManager;

        public DbConnection? Connection { get => _connection; set => _connection = value; }
        public DbTransaction? Transaction { get => _transaction; set => _transaction = value; }
        public IDbConnectionManager ConnectionManager { get => _connectionManager; set => _connectionManager = value; }

        public AbstractContextFactory1(IDbConnectionManager dbConnectionManager)
        {
            _connectionManager = dbConnectionManager;
        }

        public async ValueTask CreateConnectionAndTransactionAsync(ISQLConnectionString connectionString, System.Data.IsolationLevel isolationLevel, CancellationToken ctk)
        {
            var connection = await ConnectionManager.GetAsync(connectionString.c, ctk);

            if (Connection?.State != ConnectionState.Open)
            {
                if (Connection?.State == ConnectionState.Closed)
                    await Connection.OpenAsync(ctk);
            }
            if (Transaction == null)
            {
                Transaction = Connection?.BeginTransactionAsync(isolationLevel, ctk).GetAwaiter().GetResult();
            }
        }
    }
}
