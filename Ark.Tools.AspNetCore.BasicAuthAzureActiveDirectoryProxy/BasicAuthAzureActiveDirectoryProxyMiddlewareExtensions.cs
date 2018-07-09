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
