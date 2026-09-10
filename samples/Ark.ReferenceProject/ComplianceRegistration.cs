using Ark.Tools.Compliance;

namespace Ark.ReferenceProject;

internal static class ComplianceRegistration
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddArkRedaction();
    }
}
