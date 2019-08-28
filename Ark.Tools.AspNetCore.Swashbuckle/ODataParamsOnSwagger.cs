// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Swashbuckle.AspNetCore.Swagger;
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
		public void Apply(Operation operation, OperationFilterContext context)
		{
			if (operation.Parameters == null)
				return;

			var hasAttribute = context.MethodInfo.GetCustomAttributes(typeof(SwaggerAddODataParamsAttribute), true).Length > 0;

			if (hasAttribute)
			{
				hasAttribute = false;

				operation.Parameters.Add(new NonBodyParameter
				{
					Name = "$filter",
					Description = "Filter the results using OData syntax.",
					Required = false,
					Type = "string",
					In = "query"
				});

				operation.Parameters.Add(new NonBodyParameter
				{
					Name = "$orderby",
					Description = "Order the results using OData syntax.",
					Required = false,
					Type = "string",
					In = "query"
				});

				operation.Parameters.Add(new NonBodyParameter
				{
					Name = "$skip",
					Description = "The number of results to skip.",
					Required = false,
					Type = "integer",
					In = "query"
				});

				operation.Parameters.Add(new NonBodyParameter
				{
					Name = "$top",
					Description = "The number of results to return.",
					Required = false,
					Type = "integer",
					In = "query"
				});
			}
		}
	}
}
