using Ark.Tools.Compliance;
using Microsoft.IdentityModel.Tokens;

namespace Ark.Reference.Core.Tests.Auth;

public static class JwtSecurityKey
{
    public static SymmetricSecurityKey Create([InfrastructureSecret] string secret)
    {
        return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));
    }
}