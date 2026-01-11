using Ark.Tools.Solid;




using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class Post_PolymorphicRequestHandler : IRequestHandler<Post_PolymorphicRequest.V1, Polymorphic?>
{
    public Polymorphic? Execute(Post_PolymorphicRequest.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<Polymorphic?> ExecuteAsync(Post_PolymorphicRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await Task.FromResult(request.Entity).ConfigureAwait(false);
    }
}
