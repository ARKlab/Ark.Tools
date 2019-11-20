using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;
using Ark.Tools.Core;
using Microsoft.OpenApi.Models;

namespace RavenDbSample.Utils
{
	public class SwaggerExcludeFilter : ISchemaFilter
	{
		public void Apply(OpenApiSchema schema, SchemaFilterContext context)
		{
			if (schema?.Properties == null || context.GetType() == null)
				return;

			if (typeof(IAuditableEntity).IsAssignableFrom(context.GetType()))
			{
				var name = char.ToLower(nameof(IAuditableEntity.AuditId)[0]) + nameof(IAuditableEntity.AuditId).Substring(1);

				if (schema.Properties.ContainsKey(name))
					schema.Properties.Remove(name);
			}
		}
	}
}
