using System;
using System.IdentityModel.Tokens.Jwt;

namespace Ark.Tools.SpecFlow.Auth
{
    public sealed class JwtToken
    {
        private JwtSecurityToken _token;

        internal JwtToken(JwtSecurityToken token)
        {
            this._token = token;
        }

        public DateTime ValidTo => _token.ValidTo;
        public string Value => new JwtSecurityTokenHandler().WriteToken(this._token);
    }
}
