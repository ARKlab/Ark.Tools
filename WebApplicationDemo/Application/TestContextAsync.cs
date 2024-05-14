using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Sql;

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplicationDemo.Application
{
    public class TestContextAsync : SqlContextAsyncFactory<ITestContext>
    {
        public TestContextAsync(IDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager) : 
            base(dbConnectionManager, contextConfig.SqlConnectionString)
        {
        }

        public override ITestContext Create(DbConnection dbConnection, DbTransaction dbTransaction)
        {
            var ctx = new TestContext(dbConnection, dbTransaction);

            return ctx;
        }
    }


    public interface ITestContext : ISqlContextAsync, IAsyncDisposable
    { 

    }

    public class TestContext : ITestContext
    {

        public DbConnection Connection { get; set; }

        public DbTransaction? Transaction { get; set; }

        public TestContext(DbConnection dbConnection, DbTransaction dbTransaction)
        {
            Connection = dbConnection;
            Transaction = dbTransaction;
        }

        public async ValueTask ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk)
        {
            Transaction = await Connection.BeginTransactionAsync(isolationLevel, ctk);
        }

        public async ValueTask CommitAysnc(CancellationToken ctk)
        {
            if (Transaction != null)
            {
                await Transaction.CommitAsync(ctk);
            }
            else
            {
                Transaction = await Connection.BeginTransactionAsync(IsolationLevel.Unspecified, ctk);

                await Transaction.CommitAsync(ctk);
            }
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            if (Connection != null && Transaction != null) 
            { 
                await Connection.DisposeAsync();
                await Transaction.DisposeAsync();
            }

            if (Connection != null) 
            { 
                await Connection.DisposeAsync();
            }

            if (Transaction != null) 
            { 
                await Transaction.DisposeAsync();
            }
        }

        public async ValueTask RollbackAsync(CancellationToken ctk)
        {
            if (Transaction != null)
                await Transaction.RollbackAsync(ctk);
        }
    }
}
