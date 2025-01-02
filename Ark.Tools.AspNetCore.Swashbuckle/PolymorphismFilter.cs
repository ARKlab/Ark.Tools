// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    [Obsolete("Use SelectDiscriminatorName/Value from SwaggerGen options. See https://github.com/domaindrivendev/Swashbuckle.AspNetCore#inheritance-and-polymorphism")]
    public class PolymorphismSchemaFilter<T> : ISchemaFilter
    {
        private readonly HashSet<Type> _derivedTypes;

        public PolymorphismSchemaFilter(HashSet<Type> derivedTypes)
        {
            _derivedTypes = derivedTypes;
        }

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (!_derivedTypes.Contains(context.Type))
                return;

            var parentSchemaRef = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
            var parentSchema = context.SchemaRepository.Schemas[parentSchemaRef.Reference.Id.Split('/').Last()];

            foreach (var p in parentSchema.Properties.Keys)
                schema.Properties.Remove(p);

            schema.AllOf = new List<OpenApiSchema> { parentSchemaRef };
        }
    }

    [Obsolete("Use SelectDiscriminatorName/Value from SwaggerGen options. See https://github.com/domaindrivendev/Swashbuckle.AspNetCore#inheritance-and-polymorphism")]
    public class PolymorphismDocumentFilter<T> : IDocumentFilter
    {
        private readonly string _discriminatorName;
        private readonly HashSet<Type> _derivedTypes;

        public PolymorphismDocumentFilter(string fieldDiscriminatorName, HashSet<Type> derivedTypes)
        {
            _discriminatorName = fieldDiscriminatorName ?? "discriminator";
            _derivedTypes = derivedTypes
                ?? new HashSet<Type>(typeof(T).Assembly
                .GetTypes()
                .Where(x => typeof(T) != x && typeof(T).IsAssignableFrom(x)));
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var parentSchema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
            if (parentSchema.Reference != null)
                parentSchema = context.SchemaRepository.Schemas[parentSchema.Reference.Id.Split('/').Last()];

            //set up a discriminator property (it must be required)
            parentSchema.Discriminator = new OpenApiDiscriminator
            {
                PropertyName = _discriminatorName
            };

            parentSchema.Required = parentSchema.Required ?? new HashSet<string>(StringComparer.Ordinal);
            parentSchema.Required.Add(_discriminatorName);

            if (!parentSchema.Properties.ContainsKey(_discriminatorName))
                parentSchema.Properties.Add(_discriminatorName, new OpenApiSchema { Type = "string" });

            foreach (var item in _derivedTypes)
                context.SchemaGenerator.GenerateSchema(item, context.SchemaRepository);

        }
    }

    public class TestFilter : IDocumentFilter
    {
        private readonly string _param1;
        private readonly int _param2;

        public TestFilter(string param1, int param2)
        {
            _param1 = param1;
            _param2 = param2;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {


        }
    }
}
