using Hellang.Middleware.ProblemDetails;
using Microsoft.Extensions.Options;

namespace Core.Service.WebInterface.Utils
{
    public class ConfigureProblemDetails : IConfigureOptions<ProblemDetailsOptions>
    {
        public void Configure(ProblemDetailsOptions options)
        {
        }
    }
}
