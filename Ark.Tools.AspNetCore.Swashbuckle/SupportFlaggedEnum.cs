// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public class SupportFlaggedEnums : IOperationFilter
    {
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
		{
			if (operation.Parameters == null) 
				return;

			var queryEnumParams = operation.Parameters.OfType<OpenApiParameter>()
				.Where(param => param.In == ParameterLocation.Query)
				.Join(context.ApiDescription.ParameterDescriptions, o => o.Name, i => i.Name, (o, i) => new { o, i })
				.Where(x =>
				{
					var t = x.i.Type;
					if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
					{
						t = Nullable.GetUnderlyingType(t) ?? t;
					}
					return t.IsEnum && t.IsDefined(typeof(FlagsAttribute), false);
				})
				.Select(x => x.o)
				.ToArray();

			foreach (var param in queryEnumParams)
			{
				var s = param.Schema;
				param.Schema = new OpenApiSchema { Type = "array", Items = s };
				param.Style = ParameterStyle.Simple;
				param.Explode = false;
			}


		}
	}
}
