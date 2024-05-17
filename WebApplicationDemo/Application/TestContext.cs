using Ark.Tools.Sql;

using System.Data;
using System.Data.Common;

namespace WebApplicationDemo.Application
{
    public class TestContext : AbstractSqlContextAsync<ISqlContextAsync>, ISqlContextAsync
    {
        public TestContext(DbConnection connection, DbTransaction transaction, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) :
            base(connection, transaction, isolationLevel)
        {

        }

        public TestContext(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) :
            base(connection, isolationLevel)
        {

        }

        public TestContext(DbTransaction transaction) :
            base(transaction)
        {

        }
    }
}
