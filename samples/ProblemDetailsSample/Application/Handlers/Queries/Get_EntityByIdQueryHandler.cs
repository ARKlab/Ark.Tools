using Ark.Tools.Solid;
using System;


using ProblemDetailsSample.Common.Dto;

using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Get_EntityByIdQueryHandler : IQueryHandler<Get_EntityByIdQuery.V1, Entity.V1.Output?>
    {
        public Entity.V1.Output? Execute(Get_EntityByIdQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Entity.V1.Output?> ExecuteAsync(Get_EntityByIdQuery.V1 query, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (string.Equals(query.EntityId, "ensure", StringComparison.Ordinal))
            {
                throw new ArgumentException("EntityId cannot be 'ensure'", nameof(query));
            }

            if (query.EntityId == "null") return null;

            var entity = new Entity.V1.Output()
            {
                EntityId = query.EntityId
            };

            return await Task.FromResult(entity).ConfigureAwait(false);
        }
    }
}