using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;
using Ark.Tools.Core;

namespace RavenDbSample.Utils
{
	public class SwaggerExcludeFilter : ISchemaFilter
	{
		public void Apply(Schema schema, SchemaFilterContext context)
		{
			if (schema?.Properties == null || context.SystemType == null)
				return;

			if (typeof(IAuditableEntity).IsAssignableFrom(context.SystemType))
			{
				var name = char.ToLower(nameof(IAuditableEntity.AuditId)[0]) + nameof(IAuditableEntity.AuditId).Substring(1);

				if (schema.Properties.ContainsKey(name))
					schema.Properties.Remove(name);
			}
		}
	}
}
