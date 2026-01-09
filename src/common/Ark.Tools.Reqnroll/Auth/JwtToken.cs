using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Reqnroll(net10.0)', Before:
namespace Ark.Tools.Reqnroll.Auth
{
    public sealed class JwtToken
    {
        private readonly SecurityTokenDescriptor _token;

        internal JwtToken(SecurityTokenDescriptor token)
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
=======
namespace Ark.Tools.Reqnroll.Auth;

public sealed class JwtToken
{
    private readonly SecurityTokenDescriptor _token;

    internal JwtToken(SecurityTokenDescriptor token)
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
>>>>>>> After


namespace Ark.Tools.Reqnroll.Auth;

public sealed class JwtToken
{
    private readonly SecurityTokenDescriptor _token;

    internal JwtToken(SecurityTokenDescriptor token)
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