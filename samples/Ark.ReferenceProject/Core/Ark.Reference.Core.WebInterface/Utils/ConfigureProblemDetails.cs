using Microsoft.Extensions.Options;

namespace Ark.Reference.Core.WebInterface.Utils;

public class ConfigureProblemDetails : IConfigureOptions<Hellang.Middleware.ProblemDetails.ProblemDetailsOptions>
{
    public void Configure(Hellang.Middleware.ProblemDetails.ProblemDetailsOptions options)
    {
    }
}