using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Net;

namespace Ark.Tools.AspNetCore.ProbDetails
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

        public IRouter Router { get; private set; }

        public IRouter BuildRouter(IApplicationBuilder app)
        {
            var pageRouteHandler = new RouteHandler(context =>
            {
                var typename = context.GetRouteValue("name") as string;
                var t = Type.GetType(typename);
                context.Response.ContentType = "text/html";
                var routeValues = context.GetRouteData().Values;
                return context.Response.WriteAsync(
                        $"<html><body><span>{WebUtility.HtmlEncode(t.AssemblyQualifiedName)}</span></body></html>");
            });

            var routeBuilder = new RouteBuilder(app, pageRouteHandler);

            routeBuilder.MapRoute("ProblemDetails", _template + "/{name}");

            Router = routeBuilder.Build();

            return Router;
        }
    }
}
