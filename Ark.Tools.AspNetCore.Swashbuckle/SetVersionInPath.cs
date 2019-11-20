// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class SetVersionInPaths : IDocumentFilter
	{ 
		public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
		{
			//var r = swaggerDoc.Paths
			//	.ToDictionary(
			//		path => path.Key.Replace("v{api-version}", swaggerDoc.Info.Version),
			//		path => path.Value
			//	);

			//swaggerDoc.Paths.Add(r.Keys.FirstOrDefault(), r.Values.FirstOrDefault());


			swaggerDoc.Paths.Add(swaggerDoc.Paths.Select(s => s.Key.Replace("v{api-version}", swaggerDoc.Info.Version)).FirstOrDefault()
				, swaggerDoc.Paths.Select(s => s.Value).FirstOrDefault());
		}
	}
}
