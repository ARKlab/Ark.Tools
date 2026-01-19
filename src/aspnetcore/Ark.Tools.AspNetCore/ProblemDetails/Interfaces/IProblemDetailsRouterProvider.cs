// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProblemDetails;

public interface IProblemDetailsRouterProvider
{
    IRouter? Router { get; }
    
    [RequiresUnreferencedCode("ProblemDetails router dynamically resolves type names from route parameters for diagnostic purposes.")]
    void BuildRouter(IApplicationBuilder app);
}