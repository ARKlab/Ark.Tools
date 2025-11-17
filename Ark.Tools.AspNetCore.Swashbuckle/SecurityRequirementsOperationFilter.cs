// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    [Obsolete("Use Swashbuckle configuration extensions", true, UrlFormat = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/v10.0.0/docs/configure-and-customize-swaggergen.md#add-security-definitions-and-requirements")]
    public class SecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {

        }
    }
}
