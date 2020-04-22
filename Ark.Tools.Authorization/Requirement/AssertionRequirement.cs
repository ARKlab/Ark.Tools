using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization.Requirement
{
    /// <summary>
    /// Implements an <see cref="IAuthorizationHandler"/> and <see cref="IAuthorizationRequirement"/>
    /// that takes a user specified assertion.
    /// </summary>
    public class AssertionRequirement : IAuthorizationHandler, IAuthorizationRequirement
    {
        /// <summary>
        /// Function that is called to handle this requirement.
        /// </summary>
        public Func<AuthorizationContext, Task<bool>> Handler { get; }

        /// <summary>
        /// Creates a new instance of <see cref="AssertionRequirement"/>.
        /// </summary>
        /// <param name="handler">Function that is called to handle this requirement.</param>
        public AssertionRequirement(Func<AuthorizationContext, bool> handler)
        {
            Contract.Requires(handler != null);

            Handler = context => Task.FromResult(handler(context));
        }

        /// <summary>
        /// Creates a new instance of <see cref="AssertionRequirement"/>.
        /// </summary>
        /// <param name="handler">Function that is called to handle this requirement.</param>
        public AssertionRequirement(Func<AuthorizationContext, Task<bool>> handler)
        {
            Contract.Requires(handler != null);

            Handler = handler;
        }

        /// <summary>
        /// Calls <see cref="AssertionRequirement.Handler"/> to see if authorization is allowed.
        /// </summary>
        /// <param name="context">The authorization information.</param>
        public async Task HandleAsync(AuthorizationContext context)
        {
            if (await Handler(context))
            {
                context.Succeed(this);
            }
        }
    }
}
