// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;

namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy
{
    public static class BasicAuthAzureActiveDirectoryProxyMiddlewareExtensions
    {
        public static IApplicationBuilder UseBasicAuthAzureActiveDirectoryProxy(this IApplicationBuilder app, BasicAuthAzureActiveDirectoryProxyConfig config)
        {
            return app.UseMiddleware<BasicAuthAzureActiveDirectoryProxyMiddleware>(config);
        }
    }
}
