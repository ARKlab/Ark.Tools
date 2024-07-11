using Ark.Tools.Solid;

using Flurl.Http;
using Flurl.Http.Configuration;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Dto;
using WebApplicationDemo.Services;

namespace WebApplicationDemo.Application.Handlers.Queries
{
    public class Get_PostsQueryHandler : IQueryHandler<Get_PostsQuery.V1, List<Post>>
    {
        private readonly IPostService _postService;

        public Get_PostsQueryHandler(IPostService postService)
        {
            _postService = postService;
        }

        public List<Post> Execute(Get_PostsQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<List<Post>> ExecuteAsync(Get_PostsQuery.V1 query, CancellationToken ctk = default)
        {
            return await _postService.GetPosts(ctk);
        }
    }
}
