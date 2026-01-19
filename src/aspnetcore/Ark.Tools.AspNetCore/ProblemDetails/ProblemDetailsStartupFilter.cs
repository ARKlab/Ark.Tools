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

    // NOTE: IStartupFilter.Configure interface method cannot have RequiresUnreferencedCode without breaking ASP.NET Core startup.
    // This calls IProblemDetailsRouterProvider.BuildRouter which has RequiresUnreferencedCode for dynamic type resolution
    // used in diagnostic routes. The diagnostic feature is non-critical and will gracefully degrade in trimmed applications.
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "IStartupFilter.Configure interface constraint prevents RequiresUnreferencedCode. Calls BuildRouter for diagnostic routes with Type.GetType. Diagnostic feature is non-critical and degrades gracefully if types are trimmed.")]
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            _routeProvider.BuildRouter(app);
            next(app);
        };
    }
}