using Ark.Tools.Outbox;
using Ark.Tools.Sql;

using Dapper;

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application
{
    public class TestContext : AbstractSqlContextAsync<ISqlContextAsync>, ISqlDataContext
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

        public async Task<Person?> ReadFirstEntityAsync(CancellationToken ctk = default)
        {
            var queryText = "SELECT TOP(1) ID, FirstName, LastName FROM [dbo].[People]";

            var cmd = new CommandDefinition(queryText, transaction: this.Transaction, cancellationToken: ctk);

            var person = await this.Connection.QuerySingleAsync<Person>(cmd);

            if (person == null)
                return null;

            return person;
        }

        public Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<int> ClearAsync(CancellationToken ctk = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
