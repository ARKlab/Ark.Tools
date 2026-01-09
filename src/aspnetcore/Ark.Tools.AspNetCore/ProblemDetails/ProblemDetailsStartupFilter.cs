// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;


namespace Ark.Tools.AspNetCore.ProblemDetails;

public class ProblemDetailsStartupFilter : IStartupFilter
{
    private readonly IProblemDetailsRouterProvider _routeProvider;

    public ProblemDetailsStartupFilter(IProblemDetailsRouterProvider routeProvider)
    {
        _routeProvider = routeProvider;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            _routeProvider.BuildRouter(app);
            next(app);
        };
    }
}