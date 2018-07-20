// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class RequiredSchemaFilter : ISchemaFilter
    {
        public void Apply(Schema model, SchemaFilterContext context)
        {
            var requiredProperties = context.SystemType.GetProperties()
                .Where(w => w.GetCustomAttributes(typeof(RequiredAttribute), true).Any())
                .Select(s => Char.ToLowerInvariant(s.Name[0]) + s.Name.Substring(1));

            model.Required = requiredProperties.ToList();
            if (model.Required.Count == 0)
                model.Required = null;
        }
    }
}
