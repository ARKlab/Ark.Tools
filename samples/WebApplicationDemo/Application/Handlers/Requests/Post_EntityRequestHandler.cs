using Ark.Tools.Solid;

using NodaTime;


using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class Post_EntityRequestHandler : IRequestHandler<Post_EntityRequest.V1, Entity.V1.Output>
{
    public Entity.V1.Output Execute(Post_EntityRequest.V1 request)
    {
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    public async Task<Entity.V1.Output> ExecuteAsync(Post_EntityRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new Entity.V1.Output(request)
        {
            Value = 42,
            Date = LocalDate.MinIsoValue,
        };

        return await Task.FromResult(entity).ConfigureAwait(false);
    }
}