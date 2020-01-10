// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.ComponentModel;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public class ExampleSchemaFilter<T> : ISchemaFilter
    {
        public ExampleSchemaFilter(T example)
        {
            Example = example;
        }

        public T Example { get; protected set; }

		public void Apply(OpenApiSchema schema, SchemaFilterContext context)
		{
			if (context.Type == typeof(T))
			{
				var b = OpenApiAnyFactory.CreateFor(schema, Example);

				if(b != null)
					schema.Example = b;
			}
		}
	}
}
