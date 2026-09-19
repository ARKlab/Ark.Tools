using NLog;


namespace Ark.Tools.Authorization;

using Ark.Tools.Compliance;

internal static partial class LoggingExtensions
{
    public static void UserAuthorizationSucceeded(this ILogger logger,
    [PersonalData] string username, string policyName)
    {
        logger.Trace(global::System.Globalization.CultureInfo.InvariantCulture, "Authorization for policy {PolicyName} succeeded.", policyName);
    }

    public static void UserAuthorizationFailed(this ILogger logger,
    [PersonalData] string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
    {
        logger.Trace(global::System.Globalization.CultureInfo.InvariantCulture, "Authorization for policy {PolicyName} failed. Missing requirements {FailedRequirements}", policyName, string.Join(", ", failedRequirements));
    }
}
