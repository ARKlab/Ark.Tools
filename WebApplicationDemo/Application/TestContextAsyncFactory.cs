using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Sql;

using System.Data.Common;

namespace WebApplicationDemo.Application
{
    public class TestContextAsyncFactory : SqlContextAsyncFactory<ISqlContextAsync>
    {
        public TestContextAsyncFactory(ISqlDataContextConfig contextConfig, IDbConnectionManager dbConnectionManager) : 
            base(dbConnectionManager, contextConfig.SQLConnectionString)
        {
        }

        protected override ISqlContextAsync Create(DbConnection dbConnection, DbTransaction dbTransaction)
        {
            var ctx = new TestContext(dbConnection, dbTransaction);

            return ctx;
        }
    }
}
