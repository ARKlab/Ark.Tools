// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public class AspNetCoreUserContextProvider : IContextProvider<ClaimsPrincipal?>
    {
        private readonly IHttpContextAccessor _accessor;

        public AspNetCoreUserContextProvider(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public ClaimsPrincipal? Current
        {
            get
            {
                return _accessor.HttpContext?.User;
            }
        }
    }
}
