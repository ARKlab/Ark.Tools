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
    public abstract class SqlContextAsyncFactory<T> : IContextFactory<T> where T : ISqlContextAsync
    {
        private readonly string _connectionString;
        private readonly IDbConnectionManager _dbConnectionManager;
        private readonly IsolationLevel _isolationLevel;

        public SqlContextAsyncFactory(IDbConnectionManager dbConnectionManager, string connectionString, IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted)
        {
            _dbConnectionManager = dbConnectionManager;
            _isolationLevel = isolationLevel;
            _connectionString = connectionString;
        }

        public async ValueTask<T> CreateAsync(CancellationToken cancellationToken)
        {
            DbConnection? _dbConnection;
            DbTransaction? _dbTransaction;

            _dbConnection = await _dbConnectionManager.GetAsync(_connectionString, cancellationToken);

            if (_dbConnection?.State != ConnectionState.Open)
            {
                if (_dbConnection?.State == ConnectionState.Closed)
                    await _dbConnection.OpenAsync(cancellationToken);
            }

#if !(NET472 || NETSTANDARD2_0)
            if (_dbConnection != null)
            {
                _dbTransaction = await _dbConnection.BeginTransactionAsync(_isolationLevel, cancellationToken);

                if (_dbTransaction != null)
                    return Create(_dbConnection, _dbTransaction);
            }

            throw new ArgumentException("Missing transaction");
#else
            throw new NotSupportedException("Async SQL not supported for this .NET framework");
#endif
        }

        public abstract T Create(DbConnection dbConnection, DbTransaction dbTransaction);
    }
}
