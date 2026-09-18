using NLog;


namespace Ark.Tools.Authorization;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

internal static partial class LoggingExtensions
{
    public static void UserAuthorizationSucceeded(this ILogger logger, 
#if NET10_0_OR_GREATER
    [PersonalData]
#endif
 string username, string policyName)
    {
        logger.Trace(global::System.Globalization.CultureInfo.InvariantCulture, "Authorization for policy {PolicyName} succeeded.", policyName);
    }

    public static void UserAuthorizationFailed(this ILogger logger, 
#if NET10_0_OR_GREATER
    [PersonalData]
#endif
 string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
    {
        logger.Trace(global::System.Globalization.CultureInfo.InvariantCulture, "Authorization for policy {PolicyName} failed. Missing requirements {FailedRequirements}", policyName, string.Join(", ", failedRequirements));
    }
}
