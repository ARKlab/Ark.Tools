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
    public abstract class AbstractSqlContextFactory
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private IDbConnectionManager _connectionManager;

        public DbConnection? Connection { get => _connection; set => _connection = value; }
        public DbTransaction? Transaction { get => _transaction; set => _transaction = value; }
        public IDbConnectionManager ConnectionManager { get => _connectionManager; set => _connectionManager = value; }

        public AbstractSqlContextFactory(IDbConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        //public async ValueTask CreateAsync(ISQLConnectionString connectionString, IsolationLevel isolationLevel, CancellationToken ctk)
        public async ValueTask CreateAsync(string connectionString, IsolationLevel isolationLevel, CancellationToken ctk)
        {
            var connection = await ConnectionManager.GetAsync(connectionString, ctk);

            if (Connection?.State != ConnectionState.Open)
            {
                if (Connection?.State == ConnectionState.Closed)
                    await Connection.OpenAsync(ctk);
            }

            if (Transaction == null) 
            {
                var vt = Connection?.BeginTransactionAsync(isolationLevel, ctk);

                var transaction = vt?.GetAwaiter().GetResult();

                Transaction = transaction;
            }
        }
    }
}
