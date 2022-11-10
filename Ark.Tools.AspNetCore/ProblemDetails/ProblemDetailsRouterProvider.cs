using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ProblemDetailsRouterProvider : IProblemDetailsRouterProvider
    {
        private readonly string _template;

        public ProblemDetailsRouterProvider()
        {
            _template = "problemdetails";
        }

        public ProblemDetailsRouterProvider(string template)
        {
            _template = template;
        }

        public IRouter? Router { get; private set; }

        [MemberNotNull(nameof(Router))]
        public void BuildRouter(IApplicationBuilder app)
        {
            var pageRouteHandler = new RouteHandler(context =>
            {
                var typename = context.GetRouteValue("name") as string;
                string content = "Unknown";
                if (typename != null)
                {
                    var t = Type.GetType(typename);
                    if (t != null)
                        content = t.AssemblyQualifiedName ?? content;
                }
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync(
                        $"<html><body><span>{WebUtility.HtmlEncode(content)}</span></body></html>");
            });

            var routeBuilder = new RouteBuilder(app, pageRouteHandler);

            routeBuilder.MapRoute("ProblemDetails", _template + "/{name}");

            Router = routeBuilder.Build();
        }
    }
}
