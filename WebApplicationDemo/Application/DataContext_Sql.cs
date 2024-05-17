using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Sql;

using Dapper;

using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application
{
    public class DataContextSql : AbstractSqlContextWithOutboxAsync<DataSql>, ISqlDataContext
    {
        public DataContextSql(ISqlDataContextConfig config, IDbConnectionManager connectionManager) 
            : base(connectionManager.Get(config.SQLConnectionString), config) 
        { 
        }

        public async Task<Person?> ReadFirstEntityAsync(CancellationToken ctk = default)
        {
            var queryText = "SELECT TOP(1) ID, FirstName, LastName FROM [dbo].[People]";

            var cmd = new CommandDefinition(queryText, transaction:  this.Transaction, cancellationToken: ctk);

            var person = await this.Connection.QuerySingleAsync<Person>(cmd);

            if (person == null)
                return null;

            return person;
        }
    }

    public class DataSql
    {

    }

    public interface ISqlDataContextConfig : IOutboxContextSqlConfig
    {
        public string SQLConnectionString { get; }
    }

    public class SqlDataContextConfig : ISqlDataContextConfig
    {
        public string SQLConnectionString => @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

        public string TableName => "People";

        public string SchemaName => "";
    }
}
