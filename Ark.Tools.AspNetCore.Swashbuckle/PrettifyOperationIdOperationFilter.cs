// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class PrettifyOperationIdOperationFilter : IOperationFilter
    {
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor cad)
			{
				operation.OperationId = $@"{context.ApiDescription.HttpMethod}{context.ApiDescription.RelativePath
					.Replace(@"v{api-version}", "")
					.Replace(@"/", @"_")
					.Replace(@"{", @"_")
					.Replace(@"}", @"_")}";
			}
		}
	}
}
