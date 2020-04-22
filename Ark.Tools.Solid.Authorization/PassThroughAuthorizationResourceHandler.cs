using Ark.Tools.Authorization;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Authorization
{
    internal class PassThroughAuthorizationResourceHandler<T,R> : IAuthorizationResourceHandler<T,R>
            where T : class
            where R : IAuthorizationPolicy
    {
        public Task<object> GetResouceAsync(T query)
        {
            return Task.FromResult<object>(query);
        }
    }
}