using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using Swashbuckle.AspNetCore.Swagger;

namespace Ark.AspNetCore.Swashbuckle
{
    public class SecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            operation.Security = new List<IDictionary<string, IEnumerable<string>>>();
            operation.Security.Add(
                new Dictionary<string, IEnumerable<string>>
                {
                    { "oauth2", new [] { "openid profile" } }
                });
        }
    }
}
