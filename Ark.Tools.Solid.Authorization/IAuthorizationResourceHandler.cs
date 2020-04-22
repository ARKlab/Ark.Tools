using Ark.Tools.Authorization;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Authorization
{
    public interface IAuthorizationResourceHandler<T,R> 
            where T : class 
            where R : IAuthorizationPolicy
    {
        Task<object> GetResouceAsync(T query);
    }
}