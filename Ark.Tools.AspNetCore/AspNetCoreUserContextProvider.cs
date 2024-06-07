// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid;
using Microsoft.AspNetCore.Http;

using System;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public class AspNetCoreUserContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly IHttpContextAccessor _accessor;

        public AspNetCoreUserContextProvider(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public ClaimsPrincipal Current
        {
            get
            {
                var ctx = _accessor.HttpContext;
                if (ctx is null) 
                    throw new InvalidOperationException("HttpContext is null. " +
                        "This is usually caused by trying to access the 'Current Request User' outside a Request context, " +
                        "like a background HostedService.");
                return ctx.User;
            }
        }
    }
}
