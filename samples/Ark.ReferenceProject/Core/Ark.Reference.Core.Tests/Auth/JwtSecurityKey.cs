using Microsoft.IdentityModel.Tokens;

namespace Ark.Reference.Core.Tests.Auth;

public static class JwtSecurityKey
{
    public static SymmetricSecurityKey Create(string secret)
    {
        return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));
    }
}