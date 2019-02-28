using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class ProblemDetailsRouterProvider : IProblemDetailsRouterProvider
    {
        //Inserire le OPTION ==> template

        public ProblemDetailsRouterProvider()
        {
        }

        public IRouter Router { get; private set; }

        public IRouter BuildRouter(IApplicationBuilder app)
        {
            var pageRouteHandler = new RouteHandler(context =>
            {
                var typename = context.GetRouteValue("name") as string;
                context.Response.ContentType = "text/html";
                var routeValues = context.GetRouteData().Values;
                return context.Response.WriteAsync(
                        $"<html><body><span>{WebUtility.HtmlEncode(typename)}</span></body></html>");
            });

            var routeBuilder = new RouteBuilder(app, pageRouteHandler);

            routeBuilder.MapRoute("ProblemDetails", "problemdetails/{name}");

            Router = routeBuilder.Build();

            return Router;
        }
    }
}
