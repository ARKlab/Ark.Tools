using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
    [Obsolete("Use Swashbuckle configuration extensions", true, UrlFormat = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/v10.0.0/docs/configure-and-customize-swaggergen.md#add-security-definitions-and-requirements")]
    public class AddUserImpersonationScope : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {

        }
    }
}