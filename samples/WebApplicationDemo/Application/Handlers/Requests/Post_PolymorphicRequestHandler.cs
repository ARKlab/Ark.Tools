using Ark.Tools.Solid;

using EnsureThat;

using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests
{
    public class Post_PolymorphicRequestHandler : IRequestHandler<Post_PolymorphicRequest.V1, Polymorphic?>
    {
        public Polymorphic? Execute(Post_PolymorphicRequest.V1 request)
        {
            return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<Polymorphic?> ExecuteAsync(Post_PolymorphicRequest.V1 request, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            return await Task.FromResult(request.Entity).ConfigureAwait(false);
        }
    }
}
