using System;
using System.Threading.Tasks;
using Ark.Tools.Authorization;
using System.Security.Claims;
using System.Collections.Generic;
using SimpleInjector;
using System.Reflection;

namespace Ark.Tools.Solid.Authorization
{
    public static class Ex
    {
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
            container.Collection.Register(new IAuthorizationPolicy[0]);
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
            container.Collection.Append(typeof(IAuthorizationPolicy), typeof(TPolicy));
        }
        public static void RegisterAuthorizationHandler<TPolicyHandler>(this Container container) where TPolicyHandler : class, IAuthorizationHandler
        {
            container.Collection.Append(typeof(IAuthorizationHandler), typeof(TPolicyHandler));
        }

        public static void RegisterAuthorizationPolicy(this Container container, params Assembly[] assemblies)
        {
            foreach (var policyType in container.GetTypesToRegister(typeof(IAuthorizationPolicy), assemblies))
                container.Collection.Append(typeof(IAuthorizationPolicy), policyType);
        }

        public static async Task<object> GetResourceAsync<T,P>(Container c, T query, P policy)
        {
            var queryType = query.GetType();
            var policyType = policy.GetType();
            var handlerType = typeof(IAuthorizationResourceHandler<,>).MakeGenericType(queryType, policyType);

            dynamic handler = c.GetInstance(handlerType);

            try
            {
                return await handler.GetResouceAsync((dynamic)query);
            }
            finally
            {
                IDisposable disp = handler as IDisposable;
                if (disp != null)
                    disp.Dispose();
            }
        }

        public static async Task<IAuthorizationPolicy> GetPolicyAsync(PolicyAuthorizeAttribute p, IAuthorizationPolicyProvider policyProvider)
        {
            var retVal = p.Policy;
            if (retVal == null)
            {
                if (string.IsNullOrWhiteSpace(p.PolicyName))
                    throw new ArgumentNullException(nameof(p.PolicyName));

                retVal = await policyProvider.GetPolicyAsync(p.PolicyName);
                if (retVal == null) throw new InvalidOperationException($"No policy found: {p.PolicyName}.");
            }

            return retVal;
        }
    }
}