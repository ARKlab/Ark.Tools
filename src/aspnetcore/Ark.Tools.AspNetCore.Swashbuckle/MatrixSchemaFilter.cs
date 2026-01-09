using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle;

public class MatrixSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(double?[,]) && schema is OpenApiSchema s)
        {
            s.Type = JsonSchemaType.Array;
            s.Items = context.SchemaGenerator.GenerateSchema(typeof(double?[]), context.SchemaRepository);
        }
    }
}