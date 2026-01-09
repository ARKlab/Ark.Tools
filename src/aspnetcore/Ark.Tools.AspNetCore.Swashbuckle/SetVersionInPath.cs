// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public class SetVersionInPaths : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var dict = swaggerDoc.Paths
            .ToDictionary(
                path => path.Key.Replace("v{api-version}", swaggerDoc.Info.Version, System.StringComparison.Ordinal),
                path => path.Value,
                System.StringComparer.Ordinal);


        foreach (var item in dict)
            swaggerDoc.Paths[item.Key] = item.Value;
    }
}