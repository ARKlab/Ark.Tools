using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.AspNetCore.Swashbuckle
{
    public class ExampleSchemaFilter<T> : ISchemaFilter
    {
        public ExampleSchemaFilter(T example)
        {
            Example = example;
        }

        public T Example { get; protected set; }

        public void Apply(Schema model, SchemaFilterContext context)
        {
            if (context.SystemType == typeof(T))
            {
                model.Example = Example;
            }
        }
    }
}
