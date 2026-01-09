// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Controllers;


namespace Ark.Tools.AspNetCore.Swashbuckle;

public class PrettifyOperationIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor cad)
        {
            operation.OperationId = $@"{context.ApiDescription.HttpMethod}{context.ApiDescription.RelativePath?
                .Replace(@"v{api-version}", "", System.StringComparison.Ordinal)
                .Replace(@"/", @"_", System.StringComparison.Ordinal)
                .Replace(@"{", @"_", System.StringComparison.Ordinal)
                .Replace(@"}", @"_", System.StringComparison.Ordinal)}";
        }
    }
}