using Microsoft.AspNetCore.Mvc.Controllers;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class PrettifyOperationIdOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor cad)
            {
                operation.OperationId = $@"{context.ApiDescription.HttpMethod}{context.ApiDescription.RelativePath
                    .Replace(@"v{api-version}", "")
                    .Replace(@"/", @"_")
                    .Replace(@"{", @"_")
                    .Replace(@"}", @"_")}";
            }
        }
    }
}
