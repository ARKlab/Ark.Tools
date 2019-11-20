// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class RequiredSchemaFilter : ISchemaFilter
    {
		public void Apply(OpenApiSchema schema, SchemaFilterContext context)
		{
			var requiredProperties = context.GetType().GetProperties()
				.Where(w => w.GetCustomAttributes(typeof(RequiredAttribute), true).Any())
				.Select(s => Char.ToLowerInvariant(s.Name[0]) + s.Name.Substring(1));

			schema.Required = requiredProperties.ToHashSet();
			if (schema.Required.Count == 0)
				schema.Required = null;
		}
	}
}
