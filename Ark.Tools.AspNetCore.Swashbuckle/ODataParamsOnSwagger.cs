// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class SwaggerAddODataParamsAttribute : Attribute
	{
	}

	public class ODataParamsOnSwagger : IOperationFilter
	{
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			if (operation.Parameters == null)
				return;

			var hasAttribute = context.MethodInfo.GetCustomAttributes(typeof(SwaggerAddODataParamsAttribute), true).Length > 0;

			if (hasAttribute)
			{
				hasAttribute = false;

				operation.Parameters.Add(new OpenApiParameter
				{
					Name = "$filter",
					Description = "Filter the results using OData syntax.",
					Required = false,
					//Type = "string",
					In = ParameterLocation.Query
				});

				operation.Parameters.Add(new OpenApiParameter
				{
					Name = "$orderby",
					Description = "Order the results using OData syntax.",
					Required = false,
					//Type = "string",
					In = ParameterLocation.Query
				});

				operation.Parameters.Add(new OpenApiParameter
				{
					Name = "$skip",
					Description = "The number of results to skip.",
					Required = false,
					//Type = "integer",
					In = ParameterLocation.Query
				});

				operation.Parameters.Add(new OpenApiParameter
				{
					Name = "$top",
					Description = "The number of results to return.",
					Required = false,
					//Type = "integer",
					In = ParameterLocation.Query
				});
			}
		}
	}
}
