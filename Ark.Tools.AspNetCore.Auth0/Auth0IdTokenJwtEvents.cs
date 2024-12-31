// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
            var ni = cid.FindFirst(ClaimTypes.NameIdentifier)?.Value?.EndsWith("@clients", StringComparison.Ordinal);
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
                    var res = await cache.GetStringAsync(cacheKey, token: ctx.HttpContext.RequestAborted).ConfigureAwait(false);
                    if (res == null)
                    {

#pragma warning disable MA0026 // Fix TODO comment
                        // TODO extract domain from autority
                        if (_isDelegation(jwt))
                        {
                            using var auth0 = new ManagementApiClient(token, _domain);
                            profile = await auth0.Users.GetAsync(jwt.Subject, cancellationToken: ctx.HttpContext.RequestAborted).ConfigureAwait(false);
                        }
                        else
                        {
                            //var auth0 = new AuthenticationApiClient(_domain);
                            //profile = await auth0.Connection.PostAsync<User>("tokeninfo", new
                            //{
                            //    id_token = token
                            //}, null, null, null, null, null);
                        }
#pragma warning restore MA0026 // Fix TODO comment

                        if (profile != null)
                        {
                            await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(profile), new DistributedCacheEntryOptions
                            {
                                AbsoluteExpiration = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero)
                            }, token: ctx.HttpContext.RequestAborted).ConfigureAwait(false);
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
            await base.TokenValidated(ctx).ConfigureAwait(false);
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

            var allRoles = new HashSet<string>(StringComparer.Ordinal);
            {
                if (profile.AppMetadata?.authorization?.roles != null)
                    allRoles.UnionWith((profile.AppMetadata?.authorization?.roles as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("authorization", out var authorization))
                    allRoles.UnionWith((authorization["roles"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("roles", out Newtonsoft.Json.Linq.JToken? roles))
                    allRoles.UnionWith((roles as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                allRoles.UnionWith(identity.FindAll("roles").Select(x => x.Value));
                identity.AddClaims(allRoles.Select(r => new Claim(identity.RoleClaimType, r, ClaimValueTypes.String, "Auth0")));
            }

            var allGroups = new HashSet<string>(StringComparer.Ordinal);
            {
                if (profile.AppMetadata?.authorization?.groups != null)
                    allGroups.UnionWith((profile.AppMetadata?.authorization?.groups as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("authorization", out Newtonsoft.Json.Linq.JToken? authorization))
                    allGroups.UnionWith((authorization["groups"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("groups", out Newtonsoft.Json.Linq.JToken? groups))
                    allGroups.UnionWith((groups as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                allGroups.UnionWith(identity.FindAll("groups").Select(x => x.Value));
                identity.AddClaims(allGroups.Select(r => new Claim(Auth0ClaimTypes.Group, r, ClaimValueTypes.String, "Auth0")));
            }

            var allPermissions = new HashSet<string>(StringComparer.Ordinal);
            {
                if (profile.AppMetadata?.authorization?.permissions != null)
                    allPermissions.UnionWith((profile.AppMetadata?.authorization?.permissions as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("authorization", out Newtonsoft.Json.Linq.JToken? authorization))
                    allPermissions.UnionWith((authorization["permissions"] as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                if (profile.ProviderAttributes.TryGetValue("permissions", out Newtonsoft.Json.Linq.JToken? permissions))
                    allPermissions.UnionWith((permissions as IEnumerable<dynamic>)?.Select(r => (string)r.ToString()) ?? Enumerable.Empty<string>());
                allPermissions.UnionWith(identity.FindAll("permissions").Select(x => x.Value));
                identity.AddClaims(allPermissions.Select(r => new Claim(Auth0ClaimTypes.Permission, r, ClaimValueTypes.String, "Auth0")));
            }
        }
    }

}
