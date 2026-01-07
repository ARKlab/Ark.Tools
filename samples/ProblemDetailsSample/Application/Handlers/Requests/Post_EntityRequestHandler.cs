using Ark.Tools.Solid;


using ProblemDetailsSample.Api.Requests;
using ProblemDetailsSample.Common.Dto;

using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Post_EntityRequestHandler : IRequestHandler<Post_EntityRequest.V1, Entity.V1.Output>
    {
        public Entity.V1.Output Execute(Post_EntityRequest.V1 request)
        {
            return ExecuteAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
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
}