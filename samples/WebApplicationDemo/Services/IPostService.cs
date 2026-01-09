
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Services;

public interface IPostService
{
    Task<List<Post>> GetPosts(CancellationToken ctk);
}