using Ark.Tools.Solid;




using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests;

public class Post_PolymorphicRequestHandler : IRequestHandler<Post_PolymorphicRequest.V1, Polymorphic?>
{
    public async Task<Polymorphic?> ExecuteAsync(Post_PolymorphicRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await Task.FromResult(request.Entity).ConfigureAwait(false);
    }
}
