using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Get_EntityByIdExceptionQueryHandler : IQueryHandler<Get_EntityByIdExceptionQuery.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Get_EntityByIdExceptionQuery.V1 query)
    {
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    public Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdExceptionQuery.V1 query, CancellationToken ctk = default)
    {
        throw new NotSupportedException();
    }
}