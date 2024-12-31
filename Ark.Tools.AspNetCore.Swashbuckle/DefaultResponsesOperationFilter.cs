// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public class DefaultResponsesOperationFilter : IOperationFilter
    {
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			if (!operation.Responses.ContainsKey("401"))
			{
				operation.Responses.Add("401",
					new OpenApiResponse
					{
						Description = "Unauthorized",
						Content = new Dictionary<string, OpenApiMediaType>(System.StringComparer.Ordinal)
                        {
							["application/json"] = new OpenApiMediaType
							{
								Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
							}
						}
					});
			}

			if (!operation.Responses.ContainsKey("403"))
			{
				operation.Responses.Add("403",
					new OpenApiResponse
					{
						Description = "Not enough permissions",
						Content = new Dictionary<string, OpenApiMediaType>(System.StringComparer.Ordinal)
                        {
							["application/json"] = new OpenApiMediaType
							{
								Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
							}
						}
					});
			}

			if (!operation.Responses.ContainsKey("400"))
			{
				operation.Responses.Add("400",
					new OpenApiResponse
					{
						Description = "Invalid payload",
						Content = new Dictionary<string, OpenApiMediaType>(System.StringComparer.Ordinal)
                        {
							["application/json"] = new OpenApiMediaType
							{
								Schema = context.SchemaGenerator.GenerateSchema(typeof(ValidationProblemDetails), context.SchemaRepository)
							}
						}
					});
			}

			if (!operation.Responses.ContainsKey("500"))
			{
				operation.Responses.Add("500",
					new OpenApiResponse
					{
						Description = "Internal server error. Retry later or contact support.",
						Content = new Dictionary<string, OpenApiMediaType>(System.StringComparer.Ordinal)
                        {
							["application/json"] = new OpenApiMediaType
							{
								Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
							}
						}
					});
			}
		}

	}
}
