// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ApiExplorer;

using System.Text.Json;

namespace Ark.Tools.AspNetCore.Swashbuckle;

/// <summary>
/// Represents the OpenAPI/Swashbuckle operation filter used to document the implicit API version parameter.
/// </summary>
/// <remarks>This <see cref="IOperationFilter"/> is only required due to bugs in the <see cref="SwaggerGenerator"/>.
/// Once they are fixed and published, this class can be removed.</remarks>
public class SwaggerDefaultValues : IOperationFilter
{
    /// <summary>
    /// Applies the filter to the specified operation using the given context.
    /// </summary>
    /// <param name="operation">The operation to apply the filter to.</param>
    /// <param name="context">The current operation filter context.</param>
    // NOTE: IOperationFilter.Apply interface method cannot have RequiresUnreferencedCode without breaking Swashbuckle compatibility.
    // Swagger requires reflection on API types for documentation generation. This is expected behavior for API documentation.
    // Applications using Swagger in trimmed scenarios must preserve their API types through [DynamicallyAccessedMembers] or
    // accept that Swagger documentation may be incomplete.
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "IOperationFilter.Apply interface constraint prevents RequiresUnreferencedCode. Swagger inherently requires reflection for API documentation. Types must be preserved by application or documentation will be incomplete.")]
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        operation.Deprecated |= apiDescription.IsDeprecated();

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1752#issue-663991077
        foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
        {
            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/b7cf75e7905050305b115dd96640ddd6e74c7ac9/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGenerator.cs#L383-L387
            var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString(CultureInfo.InvariantCulture);
            var response = operation.Responses?[responseKey];

            if (response is null || response.Content is null)
            {
                continue;
            }
            foreach (var contentType in response.Content.Keys)
            {
                if (!responseType.ApiResponseFormats.Any(x => x.MediaType == contentType))
                {
                    response.Content.Remove(contentType);
                }
            }
        }

        if (operation.Parameters == null)
        {
            return;
        }

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            var description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

            parameter.Description ??= description.ModelMetadata?.Description;

            if (parameter.Schema is OpenApiSchema s && s.Default == null && description.DefaultValue != null && description.ModelMetadata != null)
            {
                // REF: https://github.com/Microsoft/aspnet-api-versioning/issues/429#issuecomment-605402330
                var json = JsonSerializer.SerializeToNode(description.DefaultValue, description.ModelMetadata.ModelType, ArkSerializerOptions.JsonOptions);

                s.Default = json;
            }

            parameter.Required |= description.IsRequired;
        }
    }
}