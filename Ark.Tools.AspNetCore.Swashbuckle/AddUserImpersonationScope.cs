using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Collections.Generic;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    public class AddUserImpersonationScope : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.SecurityRequirements = new List<OpenApiSecurityRequirement>();

            var dict = new OpenApiSecurityRequirement();
            var openApiSecurityScheme = new OpenApiSecurityScheme() { Type = SecuritySchemeType.OAuth2 };
            dict.Add(openApiSecurityScheme, ["user_impersonation"]);

            swaggerDoc.SecurityRequirements.Add(dict);
        }
    }
}
