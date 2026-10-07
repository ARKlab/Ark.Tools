using System.IdentityModel.Tokens.Jwt;

using Ark.Tools.Compliance;

namespace Ark.Reference.Core.InProcessHost.Auth;

public sealed class JwtToken
{
    [InfrastructureSecret]
    private readonly JwtSecurityToken _token;

    internal JwtToken(
        [InfrastructureSecret]
        JwtSecurityToken token)
    {
        this._token = token;
    }

    public DateTime ValidTo => _token.ValidTo;
    public string Value => new JwtSecurityTokenHandler().WriteToken(this._token);
}