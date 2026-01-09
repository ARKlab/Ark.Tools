using NLog;

using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Authorization(net10.0)', Before:
namespace Ark.Tools.Authorization
{
    internal static partial class LoggingExtensions
    {
        internal static void UserAuthorizationSucceeded(this ILogger logger, string username, string policyName)
        {
            logger.Trace("Authorization for policy {PolicyName} succeded for user {Username}.", policyName, username);
        }

        internal static void UserAuthorizationFailed(this ILogger logger, string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
        {
            logger.Trace("Authorization for policy {PolicyName} failed for user {Username}. Missing requirements {FailedRequirements}", policyName, username, string.Join(", ", failedRequirements));
        }
=======
namespace Ark.Tools.Authorization;

internal static partial class LoggingExtensions
{
    internal static void UserAuthorizationSucceeded(this ILogger logger, string username, string policyName)
    {
        logger.Trace("Authorization for policy {PolicyName} succeded for user {Username}.", policyName, username);
    }

    internal static void UserAuthorizationFailed(this ILogger logger, string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
    {
        logger.Trace("Authorization for policy {PolicyName} failed for user {Username}. Missing requirements {FailedRequirements}", policyName, username, string.Join(", ", failedRequirements));
>>>>>>> After


namespace Ark.Tools.Authorization;

    internal static partial class LoggingExtensions
    {
        internal static void UserAuthorizationSucceeded(this ILogger logger, string username, string policyName)
        {
            logger.Trace("Authorization for policy {PolicyName} succeded for user {Username}.", policyName, username);
        }

        internal static void UserAuthorizationFailed(this ILogger logger, string username, string policyName, IEnumerable<IAuthorizationRequirement> failedRequirements)
        {
            logger.Trace("Authorization for policy {PolicyName} failed for user {Username}. Missing requirements {FailedRequirements}", policyName, username, string.Join(", ", failedRequirements));
        }
    }