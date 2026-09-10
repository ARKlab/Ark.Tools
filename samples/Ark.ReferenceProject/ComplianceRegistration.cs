using Ark.Tools.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.ReferenceProject;

public static class ComplianceRegistration
{
    public static IServiceCollection AddReferenceCompliance(this IServiceCollection services)
    {
        return services.AddArkRedaction();
    }
}
