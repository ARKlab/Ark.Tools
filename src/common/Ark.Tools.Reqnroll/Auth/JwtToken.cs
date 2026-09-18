#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ark.Tools.Reqnroll.Auth;

public sealed class JwtToken
{
    #if NET10_0_OR_GREATER
    [InfrastructureSecret]
    #endif
    private readonly SecurityTokenDescriptor _token;

    internal JwtToken(
#if NET10_0_OR_GREATER
    [InfrastructureSecret]
#endif
 SecurityTokenDescriptor token)
    {
        this._token = token;
    }

    public DateTime ValidTo => _token.Expires ?? DateTime.MinValue;
    public string Value
    {
        get
        {
            var handler = new JsonWebTokenHandler();
            handler.SetDefaultTimesOnTokenCreation = false;

            return handler.CreateToken(_token);
        }
    }
}