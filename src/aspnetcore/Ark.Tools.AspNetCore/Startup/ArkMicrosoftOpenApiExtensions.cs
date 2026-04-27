// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Core.Reflection;
using Ark.Tools.AspNetCore.Swashbuckle;

using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using NodaTime;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Swashbuckle.AspNetCore.Annotations;

namespace Ark.Tools.AspNetCore.Startup;

internal static class ArkMicrosoftOpenApiExtensions
{
    public static IServiceCollection AddArkMicrosoftOpenApiVersions(this IServiceCollection services, IEnumerable<ApiVersion> versions, Func<ApiVersion, OpenApiInfo> infoBuilder, Action<string, OpenApiOptions>? configureOptions = null)
    {
        foreach (var version in versions)
        {
            var documentName = ToDocumentName(version);
            var info = infoBuilder(version);

            services.AddOpenApi(documentName, options =>
            {
                options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
                options.ShouldInclude = description => string.Equals(description.GroupName, documentName, StringComparison.Ordinal);
                options.CreateSchemaReferenceId = CreateSchemaReferenceId;

                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = info;
                    return Task.CompletedTask;
                });
                options.AddSchemaTransformer(ApplyNodaTimeSchema);
                options.AddOperationTransformer(ApplySwaggerOperationAttribute);
                options.AddOperationTransformer(ApplySwaggerResponseAttributes);
                options.AddOperationTransformer(ApplySwaggerDefaultValues);
                options.AddOperationTransformer(ApplyODataMediaTypeCleanup);
                options.AddOperationTransformer(ApplyOperationId);
                options.AddOperationTransformer(ApplyFlaggedEnums);
                options.AddOperationTransformer(ApplyDefaultResponses);
                configureOptions?.Invoke(documentName, options);
            });
        }

        services.ArkConfigureSwaggerUI(c =>
        {
            foreach (var version in versions)
            {
                var documentName = ToDocumentName(version);
                c.SwaggerEndpoint($@"docs/{documentName}", $@"{documentName} Docs");
            }
        });

        return services;
    }

    public static string ToDocumentName(ApiVersion version)
        => $"v{version.ToString("VVVV", CultureInfo.InvariantCulture)}";

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "OpenAPI schema IDs are generated during document generation and follow the existing Swashbuckle naming policy.")]
    private static string CreateSchemaReferenceId(JsonTypeInfo jsonTypeInfo)
        => ReflectionHelper.GetCSTypeName(jsonTypeInfo.Type).Replace($"{jsonTypeInfo.Type.Namespace}.", string.Empty, StringComparison.Ordinal);

    public static async Task WriteOpenApiDocumentAsync(HttpContext context)
    {
        var documentName = context.Request.RouteValues["documentName"]?.ToString();
        if (string.IsNullOrWhiteSpace(documentName))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var staticDocumentPath = FindBuildGeneratedDocument(documentName);
        if (staticDocumentPath is not null)
        {
            context.Response.ContentType = "application/json";
            await context.Response.SendFileAsync(staticDocumentPath, context.RequestAborted).ConfigureAwait(false);
            return;
        }

#if DEBUG
        await WriteRuntimeGeneratedOpenApiDocumentAsync(context, documentName).ConfigureAwait(false);
#else
        context.Response.StatusCode = StatusCodes.Status404NotFound;
#endif
    }

#if DEBUG
    private static async Task WriteRuntimeGeneratedOpenApiDocumentAsync(HttpContext context, string documentName)
    {
        var provider = context.RequestServices.GetKeyedService<IOpenApiDocumentProvider>(documentName);
        if (provider is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var document = await provider.GetOpenApiDocumentAsync(context.RequestAborted).ConfigureAwait(false);
        context.Response.ContentType = "application/json";
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, context.RequestAborted).ConfigureAwait(false);
        await context.Response.WriteAsync(json, context.RequestAborted).ConfigureAwait(false);
    }
#endif

    private static string? FindBuildGeneratedDocument(string documentName)
    {
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? AppDomain.CurrentDomain.FriendlyName;
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, $"{entryAssemblyName}_{documentName}.json"),
            Path.Combine(baseDirectory, $"{entryAssemblyName}.json"),
            Path.Combine(baseDirectory, $"{documentName}.json")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static Task ApplyNodaTimeSchema(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        var schemaInfo = GetNodaTimeSchemaInfo(type);
        if (schemaInfo is null)
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.String;
        schema.Format = schemaInfo.Value.Format;
        schema.Example = schemaInfo.Value.Example;

        if (Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null)
        {
            schema.Type |= JsonSchemaType.Null;
        }

        return Task.CompletedTask;
    }

    private static (string? Format, string Example)? GetNodaTimeSchemaInfo(Type type)
    {
        if (type == typeof(LocalDate))
        {
            return ("date", "2016-01-21");
        }

        if (type == typeof(LocalDateTime))
        {
            return ("date-time", "2016-01-21T15:01:01.999999999");
        }

        if (type == typeof(Instant))
        {
            return ("date-time", "2016-01-21T15:01:01.999999999Z");
        }

        if (type == typeof(OffsetDateTime))
        {
            return ("date-time", "2016-01-21T15:01:01.999999999+02:00");
        }

        if (type == typeof(ZonedDateTime))
        {
            return (null, "2016-01-21T15:01:01.999999999+02:00 Europe/Rome");
        }

        if (type == typeof(LocalTime))
        {
            return ("time", "14:01:00.999999999");
        }

        if (type == typeof(DateTimeZone))
        {
            return (null, "Europe/Rome");
        }

        if (type == typeof(Period))
        {
            return ("duration", "P1Y2M-3DT4H");
        }

        return null;
    }

    private static Task ApplySwaggerOperationAttribute(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var attribute = GetLastActionMetadata<SwaggerOperationAttribute>(context);
        if (attribute is null)
        {
            return Task.CompletedTask;
        }

        operation.Summary = attribute.Summary ?? operation.Summary;
        operation.Description = attribute.Description ?? operation.Description;
        operation.OperationId = attribute.OperationId ?? operation.OperationId;

        if (attribute.Tags is { Length: > 0 })
        {
            operation.Tags = attribute.Tags
                .Select(tag => new OpenApiTagReference(tag, context.Document))
                .ToHashSet();
        }

        return Task.CompletedTask;
    }

    private static async Task ApplySwaggerResponseAttributes(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var attributes = GetActionMetadata<SwaggerResponseAttribute>(context).ToArray();
        if (attributes.Length == 0)
        {
            return;
        }

        operation.Responses ??= new OpenApiResponses();

        foreach (var attribute in attributes)
        {
            var responseKey = attribute.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (!operation.Responses.TryGetValue(responseKey, out var response))
            {
                response = new OpenApiResponse();
                operation.Responses.Add(responseKey, response);
            }

            response.Description = attribute.Description ?? response.Description;

            if (attribute.Type is null || attribute.Type == typeof(void))
            {
                continue;
            }

            var schema = await context.GetOrCreateSchemaAsync(attribute.Type, null, cancellationToken).ConfigureAwait(false);
            var contentTypes = attribute.ContentTypes is { Length: > 0 }
                ? attribute.ContentTypes
                : ["application/json"];

            var content = contentTypes.ToDictionary(
                contentType => contentType,
                _ => new OpenApiMediaType { Schema = schema },
                StringComparer.Ordinal);

            if (response is OpenApiResponse openApiResponse)
            {
                openApiResponse.Content = content;
            }
            else
            {
                operation.Responses[responseKey] = new OpenApiResponse
                {
                    Description = response.Description,
                    Content = content
                };
            }
        }
    }

    private static TAttribute? GetLastActionMetadata<TAttribute>(OpenApiOperationTransformerContext context)
        where TAttribute : Attribute
        => context.Description.ActionDescriptor.EndpointMetadata.OfType<TAttribute>().LastOrDefault()
            ?? (context.Description.ActionDescriptor is ControllerActionDescriptor cad ? cad.MethodInfo.GetCustomAttribute<TAttribute>() : null);

    private static IEnumerable<TAttribute> GetActionMetadata<TAttribute>(OpenApiOperationTransformerContext context)
        where TAttribute : Attribute
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<TAttribute>();
        if (context.Description.ActionDescriptor is ControllerActionDescriptor cad)
        {
            metadata = metadata.Concat(cad.MethodInfo.GetCustomAttributes<TAttribute>());
        }

        return metadata.Distinct();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Default parameter values are serialized only while generating OpenAPI metadata.")]
    private static Task ApplySwaggerDefaultValues(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var apiDescription = context.Description;
        operation.Deprecated |= apiDescription.IsDeprecated;

        if (operation.Responses is not null)
        {
            foreach (var responseType in apiDescription.SupportedResponseTypes)
            {
                var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString(CultureInfo.InvariantCulture);
                if (!operation.Responses.TryGetValue(responseKey, out var response) || response.Content is null)
                {
                    continue;
                }

                foreach (var contentType in response.Content.Keys.ToArray())
                {
                    if (!responseType.ApiResponseFormats.Any(x => string.Equals(x.MediaType, contentType, StringComparison.Ordinal)))
                    {
                        response.Content.Remove(contentType);
                    }
                }
            }
        }

        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            var description = apiDescription.ParameterDescriptions.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.Ordinal));
            if (description is null)
            {
                continue;
            }

            parameter.Description ??= description.ModelMetadata?.Description;

            if (parameter.Schema is OpenApiSchema schema && schema.Default is null && description.DefaultValue is not null && description.ModelMetadata is not null)
            {
                schema.Default = JsonSerializer.SerializeToNode(description.DefaultValue, description.ModelMetadata.ModelType, ArkSerializerOptions.JsonOptions);
            }

            parameter.Required |= description.IsRequired;
        }

        return Task.CompletedTask;
    }

    private static Task ApplyODataMediaTypeCleanup(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor cad
            || Attribute.IsDefined(cad.ControllerTypeInfo, typeof(Microsoft.AspNetCore.OData.Routing.Attributes.ODataAttributeRoutingAttribute)))
        {
            return Task.CompletedTask;
        }

        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Values)
            {
                RemoveODataMediaTypes(response.Content);
            }
        }

        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
            {
                RemoveODataMediaTypes(parameter.Content);
            }
        }

        RemoveODataMediaTypes(operation.RequestBody?.Content);

        return Task.CompletedTask;
    }

    private static void RemoveODataMediaTypes(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null)
        {
            return;
        }

        foreach (var contentType in content.Keys.ToArray())
        {
            if (contentType.Contains("odata", StringComparison.OrdinalIgnoreCase))
            {
                content.Remove(contentType);
            }
        }
    }

    private static Task ApplyOperationId(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor)
        {
            operation.OperationId = $@"{context.Description.HttpMethod}{context.Description.RelativePath?
                .Replace("v{api-version}", string.Empty, StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .Replace("{", "_", StringComparison.Ordinal)
                .Replace("}", "_", StringComparison.Ordinal)}";
        }

        return Task.CompletedTask;
    }

    private static Task ApplyFlaggedEnums(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        var queryEnumParams = operation.Parameters.OfType<OpenApiParameter>()
            .Where(param => param.In == ParameterLocation.Query)
            .Join(context.Description.ParameterDescriptions, o => o.Name, i => i.Name, (o, i) => new { o, i }, StringComparer.Ordinal)
            .Where(x =>
            {
                var type = x.i.Type;
                if (type is null)
                {
                    return false;
                }

                type = Nullable.GetUnderlyingType(type) ?? type;
                return type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false);
            })
            .Select(x => x.o)
            .ToArray();

        foreach (var param in queryEnumParams)
        {
            var schema = param.Schema;
            param.Schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = schema };
            param.Style = ParameterStyle.Simple;
            param.Explode = false;
        }

        return Task.CompletedTask;
    }

    private static async Task ApplyDefaultResponses(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        var problemDetailsSchema = await context.GetOrCreateSchemaAsync(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), null, cancellationToken).ConfigureAwait(false);
        var validationProblemDetailsSchema = await context.GetOrCreateSchemaAsync(typeof(ValidationProblemDetails), null, cancellationToken).ConfigureAwait(false);

        AddResponseIfMissing(operation, "401", "Unauthorized", problemDetailsSchema);
        AddResponseIfMissing(operation, "403", "Not enough permissions", problemDetailsSchema);
        AddResponseIfMissing(operation, "400", "Invalid payload", validationProblemDetailsSchema);
        AddResponseIfMissing(operation, "500", "Internal server error. Retry later or contact support.", problemDetailsSchema);
    }

    private static void AddResponseIfMissing(OpenApiOperation operation, string statusCode, string description, IOpenApiSchema schema)
    {
        if (operation.Responses is null || operation.Responses.ContainsKey(statusCode))
        {
            return;
        }

        operation.Responses.Add(statusCode, new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = schema
                }
            }
        });
    }
}
