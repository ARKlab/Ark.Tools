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

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "IStartupFilter.Configure interface method doesn't have RequiresUnreferencedCode. ProblemDetails router dynamically resolves type names for diagnostic purposes.")]
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            _routeProvider.BuildRouter(app);
            next(app);
        };
    }
}