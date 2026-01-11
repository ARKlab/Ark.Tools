using Ark.Tools.Core;
using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Get_EntityByIdQueryHandler : IQueryHandler<Get_EntityByIdQuery.V1, Entity.V1.Output?>
{
    public Entity.V1.Output? Execute(Get_EntityByIdQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<Entity.V1.Output?> ExecuteAsync(Get_EntityByIdQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIf(string.Equals(query.EntityId, "ensure", StringComparison.Ordinal), "EntityId cannot be 'ensure'", nameof(query));

        if (query.EntityId == "null") return null;

        var entity = new Entity.V1.Output()
        {
            EntityId = query.EntityId
        };

        return await Task.FromResult(entity).ConfigureAwait(false);
    }
}
