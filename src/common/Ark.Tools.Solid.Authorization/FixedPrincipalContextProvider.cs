using System;
using System.Security.Claims;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid.Authorization(net10.0)', Before:
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
=======
namespace Ark.Tools.Solid.Authorization;

public class FixedPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly Func<ClaimsPrincipal> _getter;

    public FixedPrincipalContextProvider(Func<ClaimsPrincipal> getter)
    {
        _getter = getter;
    }

    public ClaimsPrincipal Current => _getter();
>>>>>>> After


namespace Ark.Tools.Solid.Authorization;

public class FixedPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly Func<ClaimsPrincipal> _getter;

    public FixedPrincipalContextProvider(Func<ClaimsPrincipal> getter)
    {
        _getter = getter;
    }

    public ClaimsPrincipal Current => _getter();
}