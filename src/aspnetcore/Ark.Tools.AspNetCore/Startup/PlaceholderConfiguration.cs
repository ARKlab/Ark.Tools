using Asp.Versioning;
using Asp.Versioning.OData;

using Microsoft.OData.ModelBuilder;

namespace Ark.Tools.AspNetCore.Startup;

/// <summary>
/// Needed to avoid SwaggerGen complaining over MetadataController having 2 identical GET. 
/// Somehow if the EdmModel is empty, some logic is skipped and Swagger found the MetadataController.
/// With a non-empty model, all goes fine.
/// </summary>
public class FixSwaggerMetadataControllerGetDuplicationOnEmptyModelConfiguration : IModelConfiguration
{
    public void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
    {
        var f = builder.Function("FixSwaggerMetadataControllerGetDuplicationOnEmptyModelConfiguration");
        f.IncludeInServiceDocument = false;
        f.Returns<bool>();
    }
}