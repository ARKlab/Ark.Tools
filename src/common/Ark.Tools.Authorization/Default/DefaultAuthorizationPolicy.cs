using System.Collections.Generic;

namespace Ark.Tools.Authorization
{

    public abstract class AuthorizationPolicy : IAuthorizationPolicy
    {
        private readonly IAuthorizationPolicy _inner;

        protected AuthorizationPolicy()
        {
            var builder = new AuthorizationPolicyBuilder(this.GetType().FullName ?? this.GetType().Name);
#pragma warning disable CA2214 // Do not call overridable methods in constructors
            Build(builder);
#pragma warning restore CA2214 // Do not call overridable methods in constructors
            _inner = builder.Build();
        }

        protected abstract void Build(AuthorizationPolicyBuilder builder);

        public IReadOnlyList<IAuthorizationRequirement> Requirements { get { return _inner.Requirements; } }

        public string Name { get { return _inner.Name; } }
    }
}
