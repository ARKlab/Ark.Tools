using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization.Requirement
{
    /// <summary>
    /// Desbribes an <see cref="IAuthorizationRequirement"/> that takes a user defined assertion over a application defined user profile.
    /// Use of this kind of requirement, require a paired implementation of <see cref="IUserProfileProvider{TUser}"/>.
    /// </summary>
    /// <typeparam name="TUser">The user profile type.</typeparam>
    public abstract class ApplicationUserAssertionRequirement<TUser> : IAuthorizationRequirement
    {
        /// <summary>
        /// Evaluates the requirement againt the given <paramref name="user"/>.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="user">The user profile</param>
        /// <param name="ctk">CancellationToken</param>
        /// <returns><value>true</value> when the the requirement has been fulfilled; otherwise <value>false</value></returns>
        public abstract Task<bool> HandleAsync(AuthorizationContext context, TUser user, CancellationToken ctk = default);
    }



    /// <summary>
    /// Desbribes an <see cref="ApplicationUserAssertionRequirement{TUser}"/> that takes a user defined assertion over a application defined user profile.
    /// Use of this kind of requirement, require a paired implementation of <see cref="IUserProfileProvider{TUser}"/>.
    /// </summary>
    /// <typeparam name="TUser">The user profile type.</typeparam>
    public class FuncApplicationUserAssertionRequirement<TUser> : ApplicationUserAssertionRequirement<TUser>
    {
        private readonly Func<AuthorizationContext, TUser, Task<bool>> _handler;

        /// <summary>
        /// Creates a new instance of <see cref="FuncApplicationUserAssertionRequirement{TUser}"/>. 
        /// </summary>
        /// <param name="handler">The assertion handler.</param>
        public FuncApplicationUserAssertionRequirement(Func<AuthorizationContext, TUser, bool> handler)
        {
            _handler = (context, user) => Task.FromResult(handler(context, user));
        }

        /// <summary>
        /// Creates a new instance of <see cref="ApplicationUserAssertionRequirement{TUser}"/>.
        /// </summary>
        /// <param name="handler">The assertion handler.</param>
        public FuncApplicationUserAssertionRequirement(Func<AuthorizationContext, TUser, Task<bool>> handler)
        {
            _handler = handler;
        }

        public override Task<bool> HandleAsync(AuthorizationContext context, TUser user, CancellationToken ctk = default)
        {
            return _handler(context, user);
        }
    }

    /// <summary>
    /// Implements an <see cref="IAuthorizationHandler"/> which evaluates <see cref="ApplicationUserAssertionRequirement{TUser}"/>s.
    /// Requires an implementation of <see cref="IUserProfileProvider{TUser}"/>.
    /// </summary>
    /// <typeparam name="TUser">The user profile type.</typeparam>
    public class ApplicationUserAssertionHandler<TUser> : IAuthorizationHandler
    {
        private readonly IUserProfileProvider<TUser> _provider;


        public ApplicationUserAssertionHandler(IUserProfileProvider<TUser> provider)
        {
            _provider = provider;
        }

        public async Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
        {
            if (!context.Policy.Requirements.OfType<ApplicationUserAssertionRequirement<TUser>>().Any())
                return;

            var user = await _provider.GetProfile(context.User);
            if (user == null)
                return;

            foreach (var req in context.Policy.Requirements.OfType<ApplicationUserAssertionRequirement<TUser>>())
            {
                if (await req.HandleAsync(context, user, ctk))
                {
                    context.Succeed(req);
                }
            }
        }
    }

    /// <summary>
    /// A provider used to obtain the application's user profile to be used for evaluating requirements.
    /// </summary>
    /// <typeparam name="TUser">The user profile type.</typeparam>
    public interface IUserProfileProvider<TUser>
    {
        /// <summary>
        /// Gets the <typeparamref name="TUser"/> profile for the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to return the <typeparamref name="TUser"/> for.</param>
        /// <returns>The <typeparamref name="TUser"/>.</returns>
        Task<TUser> GetProfile(ClaimsPrincipal user);
    }


    public static partial class Ex
    {
        /// <summary>
        /// Adds an <see cref="ApplicationUserAssertionRequirement{TUser}"/> to the current instance.
        /// </summary>
        /// <typeparam name="TUser">The user profile type.</typeparam>
        /// <param name="builder">The policy builder.</param>
        /// <param name="handler">The handler to evaluate during authorization.</param>
        /// <returns></returns>
        public static AuthorizationPolicyBuilder RequireUserAssertion<TUser>(this AuthorizationPolicyBuilder builder, Func<AuthorizationContext, TUser, bool> handler)
        {
            builder.AddRequirements(new FuncApplicationUserAssertionRequirement<TUser>(handler));
            return builder;
        }

        /// <summary>
        /// Adds an <see cref="ApplicationUserAssertionRequirement{TUser}"/> to the current instance.
        /// </summary>
        /// <typeparam name="TResource">The resource type.</typeparam>
        /// <typeparam name="TUser">The user profile type.</typeparam>
        /// <param name="builder">The policy builder.</param>
        /// <param name="handler">The handler to evaluate during authorization.</param>
        /// <returns></returns>
        public static AuthorizationPolicyBuilder RequireUserAssertion<TResource, TUser>(this AuthorizationPolicyBuilder builder, Func<AuthorizationContext, TUser, bool> handler)
        {
            builder.AddRequirements(new FuncApplicationUserAssertionRequirement<TUser>(handler));
            return builder;
        }
    }
}
