using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class DefaultResponsesOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {                       
            if (!operation.Responses.ContainsKey("401"))
                operation.Responses.Add("401", new Response { Description = "Unauthorized" });

            if (!operation.Responses.ContainsKey("403"))
                operation.Responses.Add("403", new Response { Description = "Not enough permissions" });

            if (!operation.Responses.ContainsKey("400"))
                operation.Responses.Add("400", new Response { Description = "Invalid payload" });

            if (!operation.Responses.ContainsKey("500"))
                operation.Responses.Add("500", new Response { Description = "Internal server error. Retry later or contact support." });
        }
    }
}
