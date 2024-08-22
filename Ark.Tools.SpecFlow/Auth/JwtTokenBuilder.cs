﻿using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Ark.Tools.SpecFlow.Auth
{
    public sealed class JwtTokenBuilder
    {
        private SecurityKey? _securityKey = null;
        private string _subject = "";
        private string _issuer = "";
        private string _audience = "";
        private readonly List<Claim> _claims = new List<Claim>();
        private int _expiryInMinutes = 5;

        public JwtTokenBuilder AddSecurityKey(SecurityKey securityKey)
        {
            this._securityKey = securityKey;
            return this;
        }

        public JwtTokenBuilder AddSubject(string subject)
        {
            this._subject = subject;
            return this;
        }

        public JwtTokenBuilder AddIssuer(string issuer)
        {
            this._issuer = issuer;
            return this;
        }

        public JwtTokenBuilder AddAudience(string audience)
        {
            this._audience = audience;
            return this;
        }

        public JwtTokenBuilder AddClaim(string type, string value)
        {
            this._claims.Add(new Claim(type, value));
            return this;
        }

        public JwtTokenBuilder RemoveClaim(string type)
        {
            this._claims.RemoveAll(x => x.Type == type);
            return this;
        }

        public JwtTokenBuilder ClearClaims()
        {
            this._claims.Clear();
            return this;
        }

        public JwtTokenBuilder AddExpiry(int expiryInMinutes)
        {
            this._expiryInMinutes = expiryInMinutes;
            return this;
        }

        public JwtToken Build()
        {
            _ensureArguments();

            var claims = new List<Claim>
            {
              new Claim(JwtRegisteredClaimNames.Sub, this._subject),
              new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }
            .Union(this._claims);

            var token = new SecurityTokenDescriptor
            {
                Issuer = this._issuer,
                Audience = this._audience,
                Claims = claims.ToDictionary(x => x.Type, x => x.Value as object),
                NotBefore = DateTime.UtcNow,
                IssuedAt = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(_expiryInMinutes),
                SigningCredentials = new SigningCredentials(
                    this._securityKey,
                    SecurityAlgorithms.HmacSha256)
            };
            return new JwtToken(token);
        }

        #region " private "

        private void _ensureArguments()
        {
            if (this._securityKey == null)
                throw new ArgumentNullException("Security Key");

            if (string.IsNullOrEmpty(this._subject))
                throw new ArgumentNullException("Subject");

            if (string.IsNullOrEmpty(this._issuer))
                throw new ArgumentNullException("Issuer");

            if (string.IsNullOrEmpty(this._audience))
                throw new ArgumentNullException("Audience");
        }

        #endregion
    }
}
