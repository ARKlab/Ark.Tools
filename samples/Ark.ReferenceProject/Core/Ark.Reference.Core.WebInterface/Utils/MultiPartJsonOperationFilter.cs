using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

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

                if (propertyInfo != null && operation.RequestBody is not null && operation.RequestBody.Content is not null && operation.RequestBody.Content.Any())
                {
                    var mediaType = operation.RequestBody.Content.First().Value;

                    if (mediaType?.Schema is OpenApiSchema s && s.Properties is not null)
                    {
                        // Group all exploded properties.
                        var groupedProperties = s.Properties
                            .GroupBy(pair => pair.Key.Split('.')[0], StringComparer.Ordinal);

                        var schemaProperties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

                        foreach (var property in groupedProperties)
                        {
                            if (property.Key == propertyInfo.Name)
                            {
                                _addEncoding(mediaType, propertyInfo);

                                var openApiSchema = _getSchema(context, propertyInfo);
                                if (openApiSchema is null)
                                    continue;
                                schemaProperties.Add(property.Key, openApiSchema);
                            }
                            else
                            {
                                schemaProperties.Add(property.Key, property.First().Value);
                            }
                        }
                        // Override schema properties
                        s.Properties = schemaProperties;
                    }
                }
            }
        }

        /// <summary>
        /// Generate schema for propertyInfo
        /// </summary>
        /// <returns></returns>
        private IOpenApiSchema _getSchema(OperationFilterContext context, PropertyInfo propertyInfo)
        {
            if (context.SchemaRepository.TryLookupByType(propertyInfo.PropertyType, out var schema))
            {
                return schema;
            }

            _ = context.SchemaGenerator.GenerateSchema(propertyInfo.PropertyType, context.SchemaRepository);
            _ = context.SchemaRepository.TryLookupByType(propertyInfo.PropertyType, out schema);

            var s = schema.Target as OpenApiSchema;
            if (s is null) return schema;

            _addDescription(s, s.Title);
            _addExample(propertyInfo, s);

            return s;
        }

        private static void _addDescription(OpenApiSchema openApiSchema, string? SchemaDisplayName)
        {
            openApiSchema.Description += $"\n See {SchemaDisplayName} model.";
        }

        private static void _addEncoding(OpenApiMediaType mediaType, PropertyInfo propertyInfo)
        {
            if (mediaType.Encoding == null)
                mediaType.Encoding = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal);

            mediaType.Encoding = mediaType.Encoding
                .Where(pair => !pair.Key.Contains(propertyInfo.Name, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

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

            var json = JsonSerializer.SerializeToNode(example, _jsonOptions.Value.JsonSerializerOptions);
            openApiSchema.Example = json;
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
    public sealed class FromJsonAttribute : FromFormAttribute { }
}