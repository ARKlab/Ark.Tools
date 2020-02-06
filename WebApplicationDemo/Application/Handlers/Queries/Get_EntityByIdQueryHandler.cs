using Ark.Tools.Solid;
using EnsureThat;
using WebApplicationDemo.Dto;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebApplicationDemo.Api.Queries
{
    public class Get_EntityByIdQueryHandler : IQueryHandler<Get_EntityByIdQuery.V1, Entity.V1.Output>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Entity.V1.Output Execute(Get_EntityByIdQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));
            
            if (query.EntityId == "null") 
				return null;

            var entity = new Entity.V1.Output()
            {
                EntityId = query.EntityId
            };

            _logger.Info($"Entity {entity.EntityId} found!");

            return await Task.FromResult(entity);
        }
    }
}
