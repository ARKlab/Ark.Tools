using Ark.Tools.Solid;


using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Dto;
using WebApplicationDemo.Services;

namespace WebApplicationDemo.Application.Handlers.Queries;

public class Get_PostsQueryHandler : IQueryHandler<Get_PostsQuery.V1, List<Post>>
{
    private readonly IPostService _postService;

    public Get_PostsQueryHandler(IPostService postService)
    {
        _postService = postService;
    }

    public List<Post> Execute(Get_PostsQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(query).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<List<Post>> ExecuteAsync(Get_PostsQuery.V1 query, CancellationToken ctk = default)
    {
        return await _postService.GetPosts(ctk).ConfigureAwait(false);
    }
}
