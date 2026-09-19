using Ark.Tools.Compliance;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ark.Tools.Reqnroll.Auth;

public sealed class JwtToken
{
    [Secret]
    private readonly SecurityTokenDescriptor _token;

    internal JwtToken(
        [Secret] SecurityTokenDescriptor token)
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