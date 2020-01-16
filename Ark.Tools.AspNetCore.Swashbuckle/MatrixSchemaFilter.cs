using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class MatrixSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(double?[,]))
            {
                schema.Type = @"array";
                schema.Items = context.SchemaGenerator.GenerateSchema(typeof(double?[]), context.SchemaRepository);
            }
        }
    }
}
