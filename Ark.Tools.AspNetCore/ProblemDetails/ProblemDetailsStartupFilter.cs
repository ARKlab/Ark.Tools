// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ProblemDetailsStartupFilter : IStartupFilter
    {
        private IProblemDetailsRouterProvider _routeProvider;

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
}
