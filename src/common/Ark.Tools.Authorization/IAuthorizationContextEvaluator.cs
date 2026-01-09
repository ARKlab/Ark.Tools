using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Authorization(net10.0)', Before:
namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Determines whether an authorization request was successful or not.
    /// </summary>
    public interface IAuthorizationContextEvaluator
    {
        /// <summary>
        /// Evaluate the context to determine if the quthorization has passed.
        /// </summary>
        /// <param name="authContext">The authorization information.</param>
        /// <returns><value>True</value> if authorization has succeded otherwise <value>false</value>.</returns>
        (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext);
    }
=======
namespace Ark.Tools.Authorization;

/// <summary>
/// Determines whether an authorization request was successful or not.
/// </summary>
public interface IAuthorizationContextEvaluator
{
    /// <summary>
    /// Evaluate the context to determine if the quthorization has passed.
    /// </summary>
    /// <param name="authContext">The authorization information.</param>
    /// <returns><value>True</value> if authorization has succeded otherwise <value>false</value>.</returns>
    (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext);
>>>>>>> After


namespace Ark.Tools.Authorization;

/// <summary>
/// Determines whether an authorization request was successful or not.
/// </summary>
public interface IAuthorizationContextEvaluator
{
    /// <summary>
    /// Evaluate the context to determine if the quthorization has passed.
    /// </summary>
    /// <param name="authContext">The authorization information.</param>
    /// <returns><value>True</value> if authorization has succeded otherwise <value>false</value>.</returns>
    (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext);
}