// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public class SecurityRequirementsOperationFilter : IOperationFilter
    {
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			operation.Security = new List<OpenApiSecurityRequirement>();

			var dict = new OpenApiSecurityRequirement();
			var openApiSecurityScheme = new OpenApiSecurityScheme() { Type = SecuritySchemeType.OAuth2 };
			dict.Add(openApiSecurityScheme, new[] { "openid profile" });

			operation.Security.Add(dict);
		}
	}
}
