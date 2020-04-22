using NLog;
using System.Collections.Generic;

namespace Ark.Tools.Authorization
{
    internal static partial class LoggingExtensions
    {
        internal static void UserAuthorizationSucceeded(this ILogger logger, string username, string policyName)
        {
            logger.Trace($"Authorization for policy {policyName} succeded for user {username}.");
        }

        internal static void UserAuthorizationFailed(this ILogger logger, string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
        {
            logger.Trace($"Authorization for policy {policyName} failed for user {username}. Missing requirements {string.Join(", ", failedRequirements)}");
        }
    }
}
