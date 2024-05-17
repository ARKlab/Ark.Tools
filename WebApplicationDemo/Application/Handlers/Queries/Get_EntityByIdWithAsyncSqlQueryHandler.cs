using Ark.Tools.Solid;
using EnsureThat;
using WebApplicationDemo.Dto;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using WebApplicationDemo.Application;
using Dapper;
using System;
using Ark.Tools.Core;

namespace WebApplicationDemo.Api.Queries
{
    public class Get_EntityByIdWithAsyncSqlQueryHandler : IQueryHandler<Get_EntityByIdWithAsyncSqlQuery.V1, Person?>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        //private TestContextAsyncFactory _testContextAsyncFactory;
        private readonly Func<IContextFactory<ISqlDataContext>> _dataContext;

        public Get_EntityByIdWithAsyncSqlQueryHandler(
            //TestContextAsyncFactory testContextAsyncFactory,
            Func<IContextFactory<ISqlDataContext>> dataContext
            )
        {
            //_testContextAsyncFactory = testContextAsyncFactory;
            _dataContext = dataContext;
        }

        public Person? Execute(Get_EntityByIdWithAsyncSqlQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Person?> ExecuteAsync(Get_EntityByIdWithAsyncSqlQuery.V1 query, CancellationToken ctk = default)
        {
            // Need to introduce context but not sure how to do that.
            // This compiles but the Func isn't registered in the container I don't know how that's done normally
            EnsureArg.IsNotNull(query, nameof(query));
            
            await using var ctx = await _dataContext().CreateAsync(ctk);

            var p = await ctx.ReadFirstEntityAsync(ctk);

            /*
            await using var sqlCtx = await _testContextAsyncFactory.CreateAsync(ctk);

            var queryText = "SELECT TOP(1) ID, FirstName, LastName FROM [dbo].[People]";

            var cmd = new CommandDefinition(queryText, transaction: sqlCtx.Transaction, cancellationToken: ctk);

            var person = await sqlCtx.Connection.QuerySingleAsync<Person>(cmd);

            if (query.Name == "null") 
				return null;

            _logger.Info("Entity {EntityId} found!", person);

            return await Task.FromResult(person);
            */

            _logger.Info("Entity {EntityId} found!", p);

            return p;
        }
    }
}
