using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Services
{
    public interface IPostService
    {
        Task<List<Post>> GetPosts(CancellationToken ctk);
    }
}