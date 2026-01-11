using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Get_EntityByIdExceptionQueryHandler : IQueryHandler<Get_EntityByIdExceptionQuery.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Get_EntityByIdExceptionQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdExceptionQuery.V1 query, CancellationToken ctk = default)
    {
        throw new NotSupportedException();
    }
}
