// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public static partial class Ex    
    {
        public static void RegisterAuthorizationAspNetCoreUser(this Container container, IApplicationBuilder app)
        {
            container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new AspNetCoreUserContextProvider(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>()));
        }
    }
}
