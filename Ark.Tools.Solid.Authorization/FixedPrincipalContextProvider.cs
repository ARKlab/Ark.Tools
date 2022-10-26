using System;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.Authorization;
using System.Threading;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.Solid.Authorization
{
    public class FixedPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly Func<ClaimsPrincipal> _getter;

        public FixedPrincipalContextProvider(Func<ClaimsPrincipal> getter)
        {
            _getter = getter;
        }

        public ClaimsPrincipal Current => _getter();
    }
}