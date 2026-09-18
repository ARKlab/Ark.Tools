#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif
using Microsoft.IdentityModel.Tokens;

namespace Ark.Reference.Core.Tests.Auth;

public static class JwtSecurityKey
{
#if NET10_0_OR_GREATER
    public static SymmetricSecurityKey Create([InfrastructureSecret] string secret)
#else
    public static SymmetricSecurityKey Create(string secret)
#endif
    {
        return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));
    }
}