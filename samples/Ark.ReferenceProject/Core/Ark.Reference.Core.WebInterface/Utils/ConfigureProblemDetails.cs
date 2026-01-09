using Hellang.Middleware.ProblemDetails;

using Microsoft.Extensions.Options;

namespace Ark.Reference.Core.WebInterface.Utils
{
    public class ConfigureProblemDetails : IConfigureOptions<ProblemDetailsOptions>
    {
        public void Configure(ProblemDetailsOptions options)
        {
        }
    }
}