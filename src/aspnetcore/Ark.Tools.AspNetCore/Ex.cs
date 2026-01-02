// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid;

using EnsureThat;

using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;

using System.Linq;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public static partial class Ex
    {
        public static void RegisterAuthorizationAspNetCoreUser(this Container container)
        {
            container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, AspNetCoreUserContextProvider>();
        }

        /// <summary>
        /// Return true if the <see cref="IServiceCollection"/> has any <typeparamref name="TService"/> service registered.
        /// </summary>
        /// <typeparam name="TService">The service type to register with the <see cref="IServiceCollection"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to register the <typeparamref name="TService"/> with.</param>
        /// <returns>
        /// A <see cref="bool"/> specifying whether or not the <typeparamref name="TService"/>
        /// </returns>
        public static bool HasService<TService>(this IServiceCollection services) where TService : class
        {
            Ensure.Any.IsNotNull(services, nameof(services));

            return services.Any(sd => sd.ServiceType == typeof(TService));
        }
    }
}