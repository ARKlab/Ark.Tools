using Ark.Tools.Solid.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore.Authorization
{
    public class AspNetCoreUserContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly IHttpContextAccessor _accessor;

        public AspNetCoreUserContextProvider(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public ClaimsPrincipal Current
        {
            get
            {
                return _accessor.HttpContext.User;
            }
        }
    }
}
