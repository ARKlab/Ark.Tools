// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;

namespace Ark.Tools.AspNetCore.BasicAuthAuth0Proxy
{
    public static class Ex
    {
        public static IApplicationBuilder UseBasicAuthAuth0Proxy(this IApplicationBuilder app, BasicAuthAuth0ProxyConfig config)
        {
            return app.UseMiddleware<BasicAuthAuth0ProxyMiddleware>(config);
        }
    }
}
