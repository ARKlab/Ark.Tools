// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public interface IProblemDetailsRouterProvider
    {
        IRouter? Router { get; }
        void BuildRouter(IApplicationBuilder app);
    }
}