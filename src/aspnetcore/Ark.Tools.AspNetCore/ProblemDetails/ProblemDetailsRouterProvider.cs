// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using System.Net;

namespace Ark.Tools.AspNetCore.ProblemDetails;

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
    [UnconditionalSuppressMessage("Trimming", "IL2057:Unrecognized value passed to parameter of 'Type.GetType'", Justification = "ProblemDetails router dynamically resolves type names from route parameters. This is a diagnostic/debugging feature.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "ProblemDetails router uses reflection for diagnostic purposes. This is a non-critical debugging feature.")]
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
                    $"<html><body><span>{WebUtility.HtmlEncode(content)}</span></body></html>", context.RequestAborted);
        });

        var routeBuilder = new RouteBuilder(app, pageRouteHandler);

        routeBuilder.MapRoute("ProblemDetails", _template + "/{name}");

        Router = routeBuilder.Build();
    }
}