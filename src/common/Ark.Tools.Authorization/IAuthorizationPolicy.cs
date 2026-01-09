using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Authorization(net10.0)', Before:
namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Represents a collection of authorization requirements to evaluate, all of which must succeed.
    /// for authorization to succeed.
    /// </summary>
    public interface IAuthorizationPolicy
    {
        /// <summary>
        /// Gets the <see cref="IAuthorizationRequirement"/> name used to identify the policy in a set. Must be unique in an application.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a readonly list of <see cref="IAuthorizationRequirement"/>s which must succeed for
        /// this policy to be successful.
        /// </summary>
        IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
    }


=======
namespace Ark.Tools.Authorization;

/// <summary>
/// Represents a collection of authorization requirements to evaluate, all of which must succeed.
/// for authorization to succeed.
/// </summary>
public interface IAuthorizationPolicy
{
    /// <summary>
    /// Gets the <see cref="IAuthorizationRequirement"/> name used to identify the policy in a set. Must be unique in an application.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a readonly list of <see cref="IAuthorizationRequirement"/>s which must succeed for
    /// this policy to be successful.
    /// </summary>
    IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
>>>>>>> After
    namespace Ark.Tools.Authorization;

    /// <summary>
    /// Represents a collection of authorization requirements to evaluate, all of which must succeed.
    /// for authorization to succeed.
    /// </summary>
    public interface IAuthorizationPolicy
    {
        /// <summary>
        /// Gets the <see cref="IAuthorizationRequirement"/> name used to identify the policy in a set. Must be unique in an application.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a readonly list of <see cref="IAuthorizationRequirement"/>s which must succeed for
        /// this policy to be successful.
        /// </summary>
        IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
    }