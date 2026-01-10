using Ark.Tools.Core;
using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Get_EntityByIdNotFoundQueryHandler : IQueryHandler<Get_EntityByIdNotFoundQuery.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Get_EntityByIdNotFoundQuery.V1 query)
    {
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    public Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdNotFoundQuery.V1 query, CancellationToken ctk = default)
    {
        throw new EntityNotFoundException("Entity not found!");
    }
}