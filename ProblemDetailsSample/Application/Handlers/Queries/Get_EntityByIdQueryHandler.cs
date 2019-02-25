using Ark.Tools.Solid;
using EnsureThat;
using NodaTime;
using ProblemDetailsSample.Common.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Get_EntityByIdQueryHandler : IQueryHandler<Get_EntityByIdQuery.V1, Entity.V1.Output>
    {
        public Entity.V1.Output Execute(Get_EntityByIdQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            Entity.V1.Output entity = default;

            if (!query.EntityId.Contains("null"))
            {
                entity = new Entity.V1.Output()
                {
                    EntityId = query.EntityId
                };
            }

            return await Task.FromResult(entity);
        }
    }
}
