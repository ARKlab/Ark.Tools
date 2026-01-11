using Ark.Tools.Core;
using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Get_EntityByIdNotFoundQueryHandler : IQueryHandler<Get_EntityByIdNotFoundQuery.V1, Entity.V1.Output>
{
    public Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdNotFoundQuery.V1 query, CancellationToken ctk = default)
    {
        throw new EntityNotFoundException("Entity not found!");
    }
}
