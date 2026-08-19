using Ark.Tools.Authorization;

using SimpleInjector;

using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;

namespace Ark.Tools.Solid.Authorization;

public static class Ex
{
    private static readonly ConcurrentDictionary<(Type queryType, Type policyType), Func<Container, object, CancellationToken, Task<object>>> _resourceHandlers = new();

    public static Task<(bool authorized, IList<string> messages)> AuthorizeAsync<TPolicy>(this IAuthorizationService service, ClaimsPrincipal user, object resource)
        where TPolicy : IAuthorizationPolicy, new()
    {
        return service.AuthorizeAsync(user, resource, new TPolicy());
    }

    public static void RegisterAuthorization(this Container container)
    {
        RegisterAuthorizationBase(container);
        RegisterAuthorizationDecorator(container);
    }

    public static void RegisterAuthorizationBase(this Container container)
    {
        container.Register<IAuthorizationPolicyProvider, ContainerAuthorizationPolicyProvider>(Lifestyle.Scoped);
        container.Register<IAuthorizationContextEvaluator, DefaultAuthorizationContextEvaluator>(Lifestyle.Scoped);
        container.Register<IAuthorizationContextFactory, DefaultAuthorizationContextFactory>(Lifestyle.Scoped);
        container.Register<IAuthorizationService, DefaultAuthorizationService>(Lifestyle.Scoped);

        container.Collection.Register<IAuthorizationHandler>(typeof(PassThroughAuthorizationHandler));
        container.Collection.Register(Array.Empty<IAuthorizationPolicy>());
        container.RegisterConditional(typeof(IAuthorizationResourceHandler<,>), typeof(PassThroughAuthorizationResourceHandler<,>), Lifestyle.Singleton,
            c => !c.Handled);
    }

    public static void RegisterAuthorizationDecorator(this Container container)
    {
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(PolicyAuthorizeQueryDecorator<,>));
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(PolicyAuthorizeRequestDecorator<,>));
        container.RegisterDecorator(typeof(ICommandHandler<>), typeof(PolicyAuthorizeCommandDecorator<>));
    }

    public static void RegisterAuthorizationPolicy<TPolicy>(this Container container) where TPolicy : class, IAuthorizationPolicy
    {
        container.Collection.Append<IAuthorizationPolicy, TPolicy>();
    }
    public static void RegisterAuthorizationHandler<TPolicyHandler>(this Container container) where TPolicyHandler : class, IAuthorizationHandler
    {
        container.Collection.Append<IAuthorizationHandler, TPolicyHandler>();
    }

    public static void RegisterAuthorizationPolicy(this Container container, params Assembly[] assemblies)
    {
        foreach (var policyType in container.GetTypesToRegister<IAuthorizationPolicy>(assemblies))
            container.Collection.Append(typeof(IAuthorizationPolicy), policyType);
    }

    [RequiresUnreferencedCode("Uses reflection for authorization resource handler dispatch. Handler types must be preserved.")]
    public static async Task<object> GetResourceAsync<TQuery, TPolicy>(Container c, TQuery query, TPolicy policy, CancellationToken ctk = default)
        where TQuery : notnull
        where TPolicy : IAuthorizationPolicy
    {
        var handler = _resourceHandlers.GetOrAdd((query.GetType(), policy.GetType()), static types =>
        {
            var method = typeof(Ex).GetMethod(nameof(_getResourceCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
            return (Func<Container, object, CancellationToken, Task<object>>)method
                .MakeGenericMethod(types.queryType, types.policyType)
                .CreateDelegate(typeof(Func<Container, object, CancellationToken, Task<object>>));
        });

        return await handler(c, query, ctk).ConfigureAwait(false);
    }

    private static async Task<object> _getResourceCoreAsync<TQuery, TPolicy>(Container c, object query, CancellationToken ctk)
        where TQuery : notnull
        where TPolicy : IAuthorizationPolicy
    {
        var handler = c.GetInstance<IAuthorizationResourceHandler<TQuery, TPolicy>>();
        using var disposable = handler as IDisposable;

        return await handler.GetResouceAsync((TQuery)query, ctk).ConfigureAwait(false);
    }

    public static async Task<IAuthorizationPolicy> GetPolicyAsync(PolicyAuthorizeAttribute p, IAuthorizationPolicyProvider policyProvider, CancellationToken ctk = default)
    {
        var retVal = p.Policy;
        if (retVal == null)
        {
            if (string.IsNullOrWhiteSpace(p.PolicyName))
                throw new ArgumentNullException(nameof(p), "PolicyName should not be null");

            retVal = await policyProvider.GetPolicyAsync(p.PolicyName!, ctk).ConfigureAwait(false);
            if (retVal == null) throw new InvalidOperationException($"No policy found: {p.PolicyName}.");
        }

        return retVal;
    }
}