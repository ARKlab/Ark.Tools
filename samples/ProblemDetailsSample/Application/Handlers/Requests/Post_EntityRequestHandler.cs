using Ark.Tools.Solid;

using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;


namespace ProblemDetailsSample.Api.Queries;

public class Post_EntityRequestHandler : IRequestHandler<Post_EntityRequest.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequest.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable VSTHRD002 // Sync wrapper for legacy API
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new Entity.V1.Output()
        {
            EntityId = request.EntityId
        };

        return await Task.FromResult(entity).ConfigureAwait(false);
    }
}
