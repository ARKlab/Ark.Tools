using Ark.Tools.Authorization.Requirement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Used for building policies during application startup.
    /// </summary>
    public class AuthorizationPolicyBuilder
    {
        private readonly string _name;
        private readonly IList<IAuthorizationRequirement> _requirements = new List<IAuthorizationRequirement>();

        /// <summary>
        /// Creates a new instance of <see cref="AuthorizationPolicyBuilder"/>
        /// </summary>
        public AuthorizationPolicyBuilder(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _name = name;
        }

        /// <summary>
        /// Adds the specified <paramref name="requirements"/> to the policy for this instance.
        /// </summary>
        /// <param name="requirements">The authorization requirements to add.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder AddRequirements(params IAuthorizationRequirement[] requirements)
        {
            foreach (var req in requirements)
            {
                _requirements.Add(req);
            }
            return this;
        }

        /// <summary>
        /// Adds the specified <typeparamref name="TRequirement"/> to the policy for this instance.
        /// </summary>
        /// <typeparam name="TRequirement">The authorization requirements to add.</typeparam>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder AddRequirement<TRequirement>()
            where TRequirement : IAuthorizationRequirement, new()
        {
            _requirements.Add(new TRequirement());
            return this;
        }

        /// <summary>
        /// Combines the specified <paramref name="policy"/> into the current instance.
        /// </summary>
        /// <param name="policy">The <see cref="IAuthorizationPolicy"/> to combine.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder Combine(IAuthorizationPolicy policy)
        {
            AddRequirements(policy.Requirements.ToArray());
            return this;
        }

        /// <summary>
        /// Adds a <see cref="ClaimsAuthorizationRequirement"/>
        /// to the current instance.
        /// </summary>
        /// <param name="claimType">The claim type required.</param>
        /// <param name="requiredValues">Values the claim must process one or more of for evaluation to succeed.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireClaim(string claimType, params string[] requiredValues)
        {
            return RequireClaim(claimType, (IEnumerable<string>)requiredValues);
        }

        /// <summary>
        /// Adds a <see cref="ClaimsAuthorizationRequirement"/>
        /// to the current instance.
        /// </summary>
        /// <param name="claimType">The claim type required.</param>
        /// <param name="requiredValues">Values the claim must process one or more of for evaluation to succeed.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireClaim(string claimType, IEnumerable<string> requiredValues)
        {
            _requirements.Add(new ClaimsAuthorizationRequirement(claimType, requiredValues));
            return this;
        }

        /// <summary>
        /// Adds a <see cref="ClaimsAuthorizationRequirement"/>
        /// to the current instance.
        /// </summary>
        /// <param name="claimType">The claim type required, with no restrictions on claim value.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireClaim(string claimType)
        {
            _requirements.Add(new ClaimsAuthorizationRequirement(claimType, allowedValues: null));
            return this;
        }

        /// <summary>
        /// Adds a <see cref="RolesAuthorizationRequirement"/>
        /// to the current instance.
        /// </summary>
        /// <param name="roles">The roles required.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireRole(params string[] roles)
        {
            return RequireRole((IEnumerable<string>)roles);
        }

        /// <summary>
        /// Adds a <see cref="RolesAuthorizationRequirement"/>
        /// to the current instance.
        /// </summary>
        /// <param name="roles">The roles required.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireRole(IEnumerable<string> roles)
        {
            _requirements.Add(new RolesAuthorizationRequirement(roles));
            return this;
        }        

        /// <summary>
        /// Adds a <see cref="DenyAnonymousAuthorizationRequirement"/> to the current instance.
        /// </summary>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireAuthenticatedUser()
        {
            _requirements.Add(new DenyAnonymousAuthorizationRequirement());
            return this;
        }

        /// <summary>
        /// Adds an <see cref="AssertionRequirement"/> to the current instance.
        /// </summary>
        /// <param name="handler">The handler to evaluate during authorization.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireAssertion(Func<AuthorizationContext, bool> handler)
        {
            _requirements.Add(new AssertionRequirement(handler));
            return this;
        }

        /// <summary>
        /// Adds an <see cref="AssertionRequirement"/> to the current instance.
        /// </summary>
        /// <param name="handler">The handler to evaluate during authorization.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public AuthorizationPolicyBuilder RequireAssertion(Func<AuthorizationContext, Task<bool>> handler)
        {
            _requirements.Add(new AssertionRequirement(handler));
            return this;
        }

        sealed class AuthorizationPolicy : IAuthorizationPolicy
        {
            public AuthorizationPolicy(string name, IEnumerable<IAuthorizationRequirement> requirements)
            {
                Name = name;
                Requirements = requirements.ToList().AsReadOnly();
            }

            public string Name { get; }
            public IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
        }
        /// <summary>
        /// Builds a new <see cref="IAuthorizationPolicy"/> from the requirements 
        /// in this instance.
        /// </summary>
        /// <returns>
        /// A new <see cref="IAuthorizationPolicy"/> built from the requirements in this instance.
        /// </returns>
        public IAuthorizationPolicy Build()
        {
            return new AuthorizationPolicy(_name, _requirements);
        }
    }
}
