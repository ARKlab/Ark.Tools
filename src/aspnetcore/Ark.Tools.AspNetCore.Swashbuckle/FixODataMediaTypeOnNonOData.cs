// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;


namespace Ark.Tools.AspNetCore.Swashbuckle;

/// <summary>
/// Represents the OpenAPI/Swashbuckle operation filter used to document the implicit API version parameter.
/// </summary>
/// <remarks>This <see cref="IOperationFilter"/> is only required due to a strange behavior on the Response MediaTypes for non-OData endpoint.
/// All Endpoints are tagged by APIExplorer with all possible Formatters but using them in the Accepct cause a 406 if not really supported by the specific Endpoint.
/// Adding OData support, cause this poisoning and if User doesn't change the Accept header when using SwaggerUI, the default Execute results in 406.
/// Remove all OData MediaType from Endpoints with are not in OData Controller.
/// </remarks>
public class FixODataMediaTypeOnNonOData : IOperationFilter
{
    /// <summary>
    /// Applies the filter to the specified operation using the given context.
    /// </summary>
    /// <param name="operation">The operation to apply the filter to.</param>
    /// <param name="context">The current operation filter context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor cad
            && !Attribute.IsDefined(cad.ControllerTypeInfo, typeof(Microsoft.AspNetCore.OData.Routing.Attributes.ODataAttributeRoutingAttribute)))
        {
            if (operation.Responses is not null)
                foreach (var response in operation.Responses.Values)
                {
                    if (response.Content is null)
                        continue;
                    // not OData (missing IsODataLike())
                    foreach (var contentType in response.Content.Keys)
                    {
                        if (contentType.Contains("odata", StringComparison.OrdinalIgnoreCase))
                            response.Content.Remove(contentType);
                    }
                }
            if (operation.Parameters is not null)
                foreach (var parameter in operation.Parameters)
                {
                    if (parameter.Content is null) continue;
                    // not OData (missing IsODataLike())
                    foreach (var contentType in parameter.Content.Keys)
                    {
                        if (contentType.Contains("odata", StringComparison.OrdinalIgnoreCase))
                            parameter.Content.Remove(contentType);
                    }
                }

            if (operation.RequestBody is not null)
            {
                if (operation.RequestBody.Content is not null)
                    foreach (var contentType in operation.RequestBody.Content.Keys)
                    {
                        if (contentType.Contains("odata", StringComparison.OrdinalIgnoreCase))
                            operation.RequestBody.Content.Remove(contentType);
                    }
            }
        }
    }
}