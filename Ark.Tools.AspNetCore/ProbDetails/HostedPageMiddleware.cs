using System;
using System.Threading.Tasks;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class HostedPageMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ProblemDetailsOptions _options;

        public HostedPageMiddleware(RequestDelegate next, IOptions<ProblemDetailsOptions> options)
        {
            _next = next;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
        }
    } 
}
