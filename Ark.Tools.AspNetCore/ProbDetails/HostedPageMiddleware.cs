using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class HostedPageMiddleware
    {
        private readonly RequestDelegate _next;

        public HostedPageMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Todo: Our logic that we need to put in when the request is coming in
            // Call the next delegate/middleware in the pipeline
            await _next(context);
            // Todo: Our logic that we need to put in when the response is going back
        }
    }
}
