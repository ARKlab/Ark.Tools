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
        private readonly Func<IContextFactory<ISqlDataContext>> _dataContext;

        public Get_EntityByIdWithAsyncSqlQueryHandler(Func<IContextFactory<ISqlDataContext>> dataContext)
        {
            _dataContext = dataContext;
        }

        public Person? Execute(Get_EntityByIdWithAsyncSqlQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Person?> ExecuteAsync(Get_EntityByIdWithAsyncSqlQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));
            
            await using var ctx = await _dataContext().CreateAsync(ctk);

            var p = await ctx.ReadFirstEntityAsync(ctk);

            _logger.Info("Entity {EntityId} found!", p);

            return p;
        }
    }
}
