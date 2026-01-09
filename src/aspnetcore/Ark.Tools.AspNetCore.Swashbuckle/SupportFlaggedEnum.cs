// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public class SupportFlaggedEnums : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null)
            return;

        var queryEnumParams = operation.Parameters.OfType<OpenApiParameter>()
            .Where(param => param.In == ParameterLocation.Query)
            .Join(context.ApiDescription.ParameterDescriptions, o => o.Name, i => i.Name, (o, i) => new { o, i }, StringComparer.Ordinal)
            .Where(x =>
            {
                var t = x.i.Type;
                if (t is null) return false;

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
            param.Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = s };
            param.Style = ParameterStyle.Simple;
            param.Explode = false;
        }


    }
}