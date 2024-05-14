using Ark.Tools.Solid;
using EnsureThat;
using WebApplicationDemo.Dto;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using WebApplicationDemo.Application;
using Dapper;

namespace WebApplicationDemo.Api.Queries
{
    public class Get_EntityByIdWithAsyncSqlQueryHandler : IQueryHandler<Get_EntityByIdWithAsyncSqlQuery.V1, Person?>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private TestContextAsync _testContextAsync;

        public Get_EntityByIdWithAsyncSqlQueryHandler(TestContextAsync testContextAsync)
        {
            _testContextAsync = testContextAsync;
        }

        public Person? Execute(Get_EntityByIdWithAsyncSqlQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Person?> ExecuteAsync(Get_EntityByIdWithAsyncSqlQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            var ctx = await _testContextAsync.CreateAsync(ctk);

            var queryText = "SELECT TOP(1) ID, FirstName, LastName FROM [dbo].[People]";

            var cmd = new CommandDefinition(queryText, transaction: ctx.Transaction, cancellationToken: ctk);

            var person = await ctx.Connection.QuerySingleAsync<Person>(cmd);

            if (query.Name == "null") 
				return null;

            _logger.Info("Entity {EntityId} found!", person);

            return await Task.FromResult(person);
        }
    }
}
