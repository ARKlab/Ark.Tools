// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public static partial class Ex    
    {
        public static void RegisterAuthorizationAspNetCoreUser(this Container container)
        {
            container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, AspNetCoreUserContextProvider>();
        }
    }
}
