using Ark.Tools.Authorization;


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