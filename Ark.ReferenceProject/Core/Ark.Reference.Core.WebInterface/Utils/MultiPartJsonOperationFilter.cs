using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Ark.Reference.Core.WebInterface.Utils
{
    /// <summary>
    /// Aggregates form fields in Swagger to one JSON field and add example.
    /// </summary>
    public class MultiPartJsonOperationFilter : IOperationFilter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<JsonOptions> _jsonOptions;

        /// <summary>
        /// Creates <see cref="MultiPartJsonOperationFilter"/>
        /// </summary>
        public MultiPartJsonOperationFilter(IServiceProvider serviceProvider, IOptions<JsonOptions> jsonOptions)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _jsonOptions = jsonOptions;
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var descriptors = context.ApiDescription.ActionDescriptor.Parameters.ToList();
            foreach (var descriptor in descriptors)
            {
                // Get property with [FromJson]
                var propertyInfo = _getPropertyInfo(descriptor);

                if (propertyInfo != null)
                {
                    var mediaType = operation.RequestBody.Content.First().Value;

                    // Group all exploded properties.
                    var groupedProperties = mediaType.Schema.Properties
                        .GroupBy(pair => pair.Key.Split('.')[0]);

                    var schemaProperties = new Dictionary<string, OpenApiSchema>();

                    foreach (var property in groupedProperties)
                    {
                        if (property.Key == propertyInfo.Name)
                        {
                            _addEncoding(mediaType, propertyInfo);

                            var openApiSchema = _getSchema(context, propertyInfo);
                            schemaProperties.Add(property.Key, openApiSchema);
                        }
                        else
                        {
                            schemaProperties.Add(property.Key, property.First().Value);
                        }
                    }
                    // Override schema properties
                    mediaType.Schema.Properties = schemaProperties;
                }
            }
        }

        /// <summary>
        /// Generate schema for propertyInfo
        /// </summary>
        /// <returns></returns>
        private OpenApiSchema _getSchema(OperationFilterContext context, PropertyInfo propertyInfo)
        {
            bool present = context.SchemaRepository.TryLookupByType(propertyInfo.PropertyType, out var schema);

            if (!present)
            {
                _ = context.SchemaGenerator.GenerateSchema(propertyInfo.PropertyType, context.SchemaRepository);
                _ = context.SchemaRepository.TryLookupByType(propertyInfo.PropertyType, out schema);

                _addDescription(schema, schema.Title);
                _addExample(propertyInfo, schema);
            }
            return schema;
        }

        private static void _addDescription(OpenApiSchema openApiSchema, string SchemaDisplayName)
        {
            openApiSchema.Description += $"\n See {SchemaDisplayName} model.";
        }

        private static void _addEncoding(OpenApiMediaType mediaType, PropertyInfo propertyInfo)
        {
            mediaType.Encoding = mediaType.Encoding
                .Where(pair => !pair.Key.ToLowerInvariant().Contains(propertyInfo.Name.ToLowerInvariant(), StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            mediaType.Encoding.Add(propertyInfo.Name, new OpenApiEncoding()
            {
                ContentType = "application/json",
                Explode = false
            });
        }

        private void _addExample(PropertyInfo propertyInfo, OpenApiSchema openApiSchema)
        {
            var example = _getExampleFor(propertyInfo.PropertyType);

            // Example do not exist. Use default.
            if (example == null)
                return;

            var json = JsonSerializer.Serialize(example, _jsonOptions.Value.JsonSerializerOptions);
            openApiSchema.Example = new OpenApiString(json);
        }

        private object? _getExampleFor(Type parameterType)
        {
            var makeGenericType = typeof(IExamplesProvider<>).MakeGenericType(parameterType);
            var method = makeGenericType.GetMethod("GetExamples");
            var exampleProvider = _serviceProvider.GetService(makeGenericType);
            // Example do not exist. Use default.
            if (exampleProvider == null)
                return null;
            var example = method?.Invoke(exampleProvider, null);
            return example;
        }

        private static PropertyInfo? _getPropertyInfo(ParameterDescriptor descriptor) =>
            descriptor.ParameterType.GetProperties()
                .SingleOrDefault(f => f.GetCustomAttribute<FromJsonAttribute>() != null);
    }


    /// <summary>
    /// Suggest that this form file should be deserialized from JSON.
    /// </summary>
    public class FromJsonAttribute : FromFormAttribute { }
}