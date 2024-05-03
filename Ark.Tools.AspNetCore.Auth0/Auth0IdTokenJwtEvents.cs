// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Auth0.ManagementApi;
using Auth0.AuthenticationApi;
using Auth0.ManagementApi.Models;
using Newtonsoft.Json;
using System;

namespace Ark.Tools.AspNetCore.Auth0
{
    [Obsolete("Do not use IdToken on server-side")]
    public class Auth0IdTokenJwtEvents : JwtBearerEvents
    {
        private readonly string _domain;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _issuer;

        public Auth0IdTokenJwtEvents(string domain, string clientId, string clientSecret)
        {
            _domain = domain;
            _clientId = clientId;
            _clientSecret = clientSecret;

            _issuer = $"https://{_domain}/";
        }

        private bool _isDelegation(JwtSecurityToken jwt)
        {
            return jwt.Claims.Any(x => x.Type == "azp");
        }

        private bool _isUnattendedClient(ClaimsIdentity cid)
        {
            var ni = cid.FindFirst(ClaimTypes.NameIdentifier)?.Value?.EndsWith("@clients");
            return ni == true;
        }

        public override async Task TokenValidated(TokenValidatedContext ctx)
        {
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();

            var jwt = (ctx.SecurityToken as JwtSecurityToken);
            var token = jwt?.RawData;
            var cacheKey = $"auth0:userInfo:{token}";
            var cid = ctx.Principal?.Identity as ClaimsIdentity;

            if (cid != null && jwt != null)
            {
                if (!_isUnattendedClient(cid))
                {
                    User? profile = null;
                    var res = await cache.GetStringAsync(cacheKey);
                    if (res == null)
                    {
                        // TODO extract domain from autority
                        if (_isDelegation(jwt))
                        {
                            using var auth0 = new ManagementApiClient(token, _domain);
                            profile = await auth0.Users.GetAsync(jwt.Subject);
                        }
                        else
                        {
                            //var auth0 = new AuthenticationApiClient(_domain);
                            //profile = await auth0.Connection.PostAsync<User>("tokeninfo", new
                            //{
                            //    id_token = token
                            //}, null, null, null, null, null);
                        }

                        if (profile != null)
                        {
                            await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(profile), new DistributedCacheEntryOptions
                            {
                                AbsoluteExpiration = jwt.ValidTo
                            });
                        }
                    }
                    else
                    {
                        profile = JsonConvert.DeserializeObject<User>(res);
                    }

                    if (profile != null)
                        _convertUserToClaims(cid, profile);
                }

                // if the identity still doesn't have a name (unattended client) add a name == nameidentifier for logging/metrics purporse
                if (string.IsNullOrWhiteSpace(cid.Name))
                {
                    cid.AddClaim(new Claim(cid.NameClaimType, jwt.Subject));
                }

                var scopes = cid.FindFirst(c => c.Type == "scope" && c.Issuer == _issuer)?.Value?.Split(' ');
                if (scopes != null)
                    cid.AddClaims(scopes.Select(r => new Claim(Auth0ClaimTypes.Scope, r, ClaimValueTypes.String, _issuer)));

                if (ctx.Options.SaveToken && token != null)
                    cid.AddClaim(new Claim("id_token", token, ClaimValueTypes.String, "Auth0"));
            }
            await base.TokenValidated(ctx);
        }

        void _convertUserToClaims(ClaimsIdentity identity, User profile)
        {
            var id = profile.Identities.Single(i => i.Provider + "|" + i.UserId == profile.UserId);

            if (!string.IsNullOrWhiteSpace(profile.Email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, profile.Email, ClaimValueTypes.String, id.Connection));
                identity.AddClaim(new Claim("email", profile.Email, ClaimValueTypes.String, id.Connection));
            }

            if (!string.IsNullOrWhiteSpace(profile.FullName))
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, profile.FullName, ClaimValueTypes.String, id.Connection));
                identity.AddClaim(new Claim("name", profile.FullName, ClaimValueTypes.String, id.Connection));
            }

            if (!string.IsNullOrWhiteSpace(profile.NickName))
                identity.AddClaim(new Claim("nickname", profile.NickName, ClaimValueTypes.String, id.Connection));
            if (!string.IsNullOrWhiteSpace(profile.FirstName))
                identity.AddClaim(new Claim("given_name", profile.FirstName, ClaimValueTypes.String, id.Connection));
            if (!string.IsNullOrWhiteSpace(profile.LastName))
                identity.AddClaim(new Claim("family_name", profile.LastName, ClaimValueTypes.String, id.Connection));
            if (!string.IsNullOrWhiteSpace(id.Connection))
                identity.AddClaim(new Claim("connection", id.Connection, ClaimValueTypes.String, id.Connection));
            if (!string.IsNullOrWhiteSpace(profile.Picture))
                identity.AddClaim(new Claim("picture", profile.Picture, ClaimValueTypes.String, id.Connection));
            if (!string.IsNullOrWhiteSpace(id.Provider))
                identity.AddClaim(new Claim("provider", id.Provider, ClaimValueTypes.String, id.Connection));

            var roles = new HashSet<string>();
            if (profile.AppMetadata?.authorization?.roles != null)
                roles.UnionWith((profile.AppMetadata?.authorization?.roles as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("authorization"))
                roles.UnionWith((profile.ProviderAttributes["authorization"]["roles"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("roles"))
                roles.UnionWith((profile.ProviderAttributes["roles"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            roles.UnionWith(identity.FindAll("roles").Select(x => x.Value));
            identity.AddClaims(roles.Select(r => new Claim(identity.RoleClaimType, r, ClaimValueTypes.String, "Auth0")));

            var groups = new HashSet<string>();
            if (profile.AppMetadata?.authorization?.groups != null)
                groups.UnionWith((profile.AppMetadata?.authorization?.groups as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("authorization"))
                groups.UnionWith((profile.ProviderAttributes["authorization"]["groups"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("groups"))
                groups.UnionWith((profile.ProviderAttributes["groups"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            groups.UnionWith(identity.FindAll("groups").Select(x => x.Value));
            identity.AddClaims(groups.Select(r => new Claim(Auth0ClaimTypes.Group, r, ClaimValueTypes.String, "Auth0")));

            var permissions = new HashSet<string>();
            if (profile.AppMetadata?.authorization?.permissions != null)
                permissions.UnionWith((profile.AppMetadata?.authorization?.permissions as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("authorization"))
                permissions.UnionWith((profile.ProviderAttributes["authorization"]["permissions"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            if (profile.ProviderAttributes.ContainsKey("permissions"))
                permissions.UnionWith((profile.ProviderAttributes["permissions"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
            permissions.UnionWith(identity.FindAll("permissions").Select(x => x.Value));
            identity.AddClaims(permissions.Select(r => new Claim(Auth0ClaimTypes.Permission, r, ClaimValueTypes.String, "Auth0")));

        }
    }
    
}
