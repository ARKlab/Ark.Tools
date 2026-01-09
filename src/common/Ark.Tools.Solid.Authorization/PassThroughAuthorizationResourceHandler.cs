using Ark.Tools.Authorization;

using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid.Authorization(net10.0)', Before:
namespace Ark.Tools.Solid.Authorization
{
    internal sealed class PassThroughAuthorizationResourceHandler<T, R> : IAuthorizationResourceHandler<T, R>
            where T : class
            where R : IAuthorizationPolicy
    {
        public Task<object> GetResouceAsync(T query, CancellationToken ctk = default)
        {
            return Task.FromResult<object>(query);
        }
=======
namespace Ark.Tools.Solid.Authorization;

internal sealed class PassThroughAuthorizationResourceHandler<T, R> : IAuthorizationResourceHandler<T, R>
        where T : class
        where R : IAuthorizationPolicy
{
    public Task<object> GetResouceAsync(T query, CancellationToken ctk = default)
    {
        return Task.FromResult<object>(query);
>>>>>>> After


namespace Ark.Tools.Solid.Authorization;

internal sealed class PassThroughAuthorizationResourceHandler<T, R> : IAuthorizationResourceHandler<T, R>
        where T : class
        where R : IAuthorizationPolicy
{
    public Task<object> GetResouceAsync(T query, CancellationToken ctk = default)
    {
        return Task.FromResult<object>(query);
    }
}