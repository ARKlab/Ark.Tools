using System.IdentityModel.Tokens.Jwt;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Reference.Core.Tests.Auth;

public sealed class JwtToken
{
#if NET10_0_OR_GREATER
    [InfrastructureSecret]
#endif
    private readonly JwtSecurityToken _token;

    internal JwtToken(
#if NET10_0_OR_GREATER
        [InfrastructureSecret]
#endif
        JwtSecurityToken token)
    {
        this._token = token;
    }

    public DateTime ValidTo => _token.ValidTo;
    public string Value => new JwtSecurityTokenHandler().WriteToken(this._token);
}