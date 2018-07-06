using Microsoft.AspNetCore.Builder;

namespace Ark.AspNetCore.BasicAuthProxy
{
    public static class BasicAuthAzureActiveDirectoryProxyMiddlewareExtensions
    {
        public static IApplicationBuilder UseBasicAuthAzureActiveDirectoryProxy(this IApplicationBuilder app, BasicAuthAzureActiveDirectoryProxyConfig config)
        {
            return app.UseMiddleware<BasicAuthAzureActiveDirectoryProxyMiddleware>(config);
        }
    }
}
