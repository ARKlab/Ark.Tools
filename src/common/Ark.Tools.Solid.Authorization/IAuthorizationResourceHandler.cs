using Ark.Tools.Authorization;


namespace Ark.Tools.Solid.Authorization;

public interface IAuthorizationResourceHandler<T, TPolicy>
        where T : class
        where TPolicy : IAuthorizationPolicy
{
    Task<object> GetResouceAsync(T query, CancellationToken ctk = default);
}