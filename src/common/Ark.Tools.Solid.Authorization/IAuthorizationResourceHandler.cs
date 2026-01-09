using Ark.Tools.Authorization;

using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid.Authorization(net10.0)', Before:
namespace Ark.Tools.Solid.Authorization
{
    public interface IAuthorizationResourceHandler<T, TPolicy>
            where T : class
            where TPolicy : IAuthorizationPolicy
    {
        Task<object> GetResouceAsync(T query, CancellationToken ctk = default);
    }
=======
namespace Ark.Tools.Solid.Authorization;

public interface IAuthorizationResourceHandler<T, TPolicy>
        where T : class
        where TPolicy : IAuthorizationPolicy
{
    Task<object> GetResouceAsync(T query, CancellationToken ctk = default);
>>>>>>> After


namespace Ark.Tools.Solid.Authorization;

public interface IAuthorizationResourceHandler<T, TPolicy>
        where T : class
        where TPolicy : IAuthorizationPolicy
{
    Task<object> GetResouceAsync(T query, CancellationToken ctk = default);
}