using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class SupportFlaggedEnums : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {

            if (operation.Parameters == null) return;

            var queryEnumParams = operation.Parameters.OfType<NonBodyParameter>()
                .Where(param => param.In == "query")
                .Join(context.ApiDescription.ParameterDescriptions, o => o.Name, i => i.Name, (o, i) => new { o, i })
                .Where(x => {
                    var t = x.i.Type;
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        t = Nullable.GetUnderlyingType(t);
                    }
                    return t.IsEnum && t.IsDefined(typeof(FlagsAttribute), false);
                    })
                .Select(x => x.o)
                .ToArray();

            foreach (var param in queryEnumParams)
            {
                param.Items = new PartialSchema { Type = param.Type, Enum = param.Enum };
                param.Type = "array";
                param.CollectionFormat = "csv";
            }
        }
    }
}
