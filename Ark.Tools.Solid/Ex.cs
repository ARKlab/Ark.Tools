using Ark.Tools.Core;
using System.Security.Claims;

namespace Ark.Tools.Solid
{
    public static class Ex
    {
        public static string? GetUserId(this IContextProvider<ClaimsPrincipal> context)
        {
            return context.Current?.GetUserId();
        }
    }
}
