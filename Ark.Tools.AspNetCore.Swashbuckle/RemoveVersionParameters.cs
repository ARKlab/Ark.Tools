// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class RemoveVersionParameters : IOperationFilter
    {
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			// Remove version parameter from all Operations
			var versionParameter = operation.Parameters.Single(p => p.Name == "api-version");
			operation.Parameters.Remove(versionParameter);
		}
	}
}
