// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Ark.Tools.AspNetCore.Auth0
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "AuthenticationApiClient is a HttpClient which is long-lived")]
    public class Auth0AccessTokenJwtEvents : JwtBearerEvents
    {
        private readonly AuthenticationApiClient _auth0;
        public const string AuthorizationExtensionAudience = "urn:auth0-authz-api";
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _authzApiUrl;
        private readonly string _domain;
        private readonly string _issuer;

        public Auth0AccessTokenJwtEvents(string domain, string clientId, string clientSecret, string authzApiUrl)
        {
            _auth0 = new AuthenticationApiClient(domain);
            _domain = domain;
            _issuer = $"https://{_domain}/";
            _clientId = clientId;
            _clientSecret = clientSecret;
            _authzApiUrl = authzApiUrl;
        }

        private async Task<string> _getAuthzToken(IDistributedCache cache, CancellationToken ctk)
        {
            var cacheKey = "auth0:authzToken";
            var res = await cache.GetStringAsync(cacheKey, ctk).ConfigureAwait(false);
            if (res != null)
                return res;

            // refresh
            var resp = await _auth0.GetTokenAsync(new ClientCredentialsTokenRequest()
            {
                Audience = AuthorizationExtensionAudience,
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },ctk).ConfigureAwait(false);

            await cache.SetStringAsync(cacheKey, resp.AccessToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(resp.ExpiresIn - 2*60)
            },ctk).ConfigureAwait(false);

            return resp.AccessToken;
        }

        record PolicyResult
        {
            public IList<string> Groups { get; set; } = new List<string>();
            public IList<string> Roles { get; set; } = new List<string>();
            public IList<string> Permissions { get; set; } = new List<string>();
        }

        record CacheEntry
        {
            public CacheEntry(UserInfo userInfo)
            {
                UserInfo = userInfo;
            }

            public UserInfo UserInfo { get; set; }

            public PolicyResult? Policy { get; set; }
        }

        public override async Task TokenValidated(TokenValidatedContext ctx)
        {
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
            var ctk = ctx.HttpContext.RequestAborted;

            var jwt = (ctx.SecurityToken as JwtSecurityToken);
            var cid = ctx.Principal?.Identity as ClaimsIdentity;
            if (cid != null && jwt != null)
            {
                var token = jwt.RawData;
                var cacheKey = $"auth0:userInfo:{token}";
                if (_shouldGetRoles() && !_isUnattendedClient(cid))
                {
                    CacheEntry? cacheEntry = null;
                    var res = await cache.GetStringAsync(cacheKey, ctk).ConfigureAwait(false);
                    if (res == null)
                    {
                        // HACK:  "Pending" is a poor way to limit the requests to Auth0 API. But works for now.
                        var t1 = cache.SetStringAsync(cacheKey, "Pending", new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                        }, ctk);

                        var userInfo = await _auth0.GetUserInfoAsync(token, ctk).ConfigureAwait(false);
                        var policyPayload = jwt.Claims.FirstOrDefault(x => x.Type == Auth0ClaimTypes.PolicyPostPayload)?.Value;

                        if (userInfo != null)
                        {
                            cacheEntry = new CacheEntry(userInfo);

                            if (policyPayload != null)
                            {
                                var authzToken = await _getAuthzToken(cache, ctk).ConfigureAwait(false);
                                var url = $"{_authzApiUrl}/api/users/{WebUtility.UrlEncode(userInfo.UserId)}/policy/{WebUtility.UrlEncode(_clientId)}";

                                using var client = new HttpClient();
                                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                                req.Content = new StringContent(policyPayload, Encoding.UTF8, "application/json");
                                req.Headers.Add("Authorization", "Bearer " + authzToken);

                                var policyRes = await client.SendAsync(req, ctk).ConfigureAwait(false);

                                if (policyRes.IsSuccessStatusCode)
                                {
                                    var policyJson = await policyRes.Content.ReadAsStringAsync(ctk).ConfigureAwait(false);
                                    var policy = JsonConvert.DeserializeObject<PolicyResult>(policyJson);
                                    if (policy != null)
                                    {
                                        cacheEntry.Policy = policy;
                                    }
                                    else
                                    {
                                        cacheEntry = null;
                                    }
                                }
                                else
                                {
                                    cacheEntry = null;
                                }
                            }
                        }

                        await t1.ConfigureAwait(false);

                        if (cacheEntry != null)
                        {
                            await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(cacheEntry), new DistributedCacheEntryOptions
                            {
                                AbsoluteExpiration = new DateTimeOffset(jwt.ValidTo - TimeSpan.FromMinutes(2), TimeSpan.Zero)
                            }, ctk).ConfigureAwait(false);
                        }
                        else
                        {
                            await cache.RemoveAsync(cacheKey, ctk).ConfigureAwait(false);
                        }

                    }
                    else if (res == "Pending") // wait for the cache to be populated
                    {
                        res = await Policy.HandleResult<string>(r => r == "Pending")
                            .WaitAndRetryForeverAsync(x => TimeSpan.FromMilliseconds(100)) // Actually cannot be greater than 5sec as key would expire returning null
                            .ExecuteAsync(async ct => await cache.GetStringAsync(cacheKey, ct).ConfigureAwait(false) ?? string.Empty, ctk)
.ConfigureAwait(false);
                    }

                    if (res != null)
                    {
                        cacheEntry = JsonConvert.DeserializeObject<CacheEntry>(res);
                    }

                    if (cacheEntry != null)
                        _convertUserToClaims(cid, cacheEntry);
                }

                // if the identity still doesn't have a name (unattended client) add a name == nameidentifier for logging/metrics purporse
                if (string.IsNullOrWhiteSpace(cid.Name))
                {
                    cid.AddClaim(new Claim(cid.NameClaimType, jwt.Subject));
                }

                var scopes = cid.FindFirst(c => c.Type == "scope" && c.Issuer == _issuer)?.Value?.Split(' ');
                if (scopes != null)
                    cid.AddClaims(scopes.Select(r => new Claim(Auth0ClaimTypes.Scope, r, ClaimValueTypes.String, _issuer)));

                //if (ctx.Options.SaveToken)
                //    cid.AddClaim(new Claim("id_token", token, ClaimValueTypes.String, "Auth0"));
            }
            await base.TokenValidated(ctx).ConfigureAwait(false);
        }

        private bool _shouldGetRoles()
        {
            return !string.IsNullOrWhiteSpace(_clientId);
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

        private void _convertUserToClaims(ClaimsIdentity identity, CacheEntry entry)
        {
            //var id = profile.Identities.Single(i => i.Provider + "|" + i.UserId == profile.UserId);
            var profile = entry.UserInfo;
            if (profile != null)
            {

                if (!string.IsNullOrWhiteSpace(profile.Email))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Email, profile.Email, ClaimValueTypes.String, _issuer));
                    identity.AddClaim(new Claim("email", profile.Email, ClaimValueTypes.String, _issuer));
                }

                if (!string.IsNullOrWhiteSpace(profile.FullName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Name, profile.FullName, ClaimValueTypes.String, _issuer));
                    identity.AddClaim(new Claim("name", profile.FullName, ClaimValueTypes.String, _issuer));
                }

                if (!string.IsNullOrWhiteSpace(profile.NickName))
                    identity.AddClaim(new Claim("nickname", profile.NickName, ClaimValueTypes.String, _issuer));
                if (!string.IsNullOrWhiteSpace(profile.FirstName))
                    identity.AddClaim(new Claim("given_name", profile.FirstName, ClaimValueTypes.String, _issuer));
                if (!string.IsNullOrWhiteSpace(profile.LastName))
                    identity.AddClaim(new Claim("family_name", profile.LastName, ClaimValueTypes.String, _issuer));
                if (!string.IsNullOrWhiteSpace(profile.Picture))
                    identity.AddClaim(new Claim("picture", profile.Picture, ClaimValueTypes.String, _issuer));
            }

            if (entry.Policy != null)
            {
                identity.AddClaims(entry.Policy.Roles.Select(r => new Claim(identity.RoleClaimType, r, ClaimValueTypes.String, _issuer)));
                identity.AddClaims(entry.Policy.Permissions.Select(r => new Claim(Auth0ClaimTypes.Permission, r, ClaimValueTypes.String, _issuer)));
            }
        }
    }
}
