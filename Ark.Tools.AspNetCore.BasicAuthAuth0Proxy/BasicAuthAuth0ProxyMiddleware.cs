// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth0;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Microsoft.AspNetCore.Http;
using Polly;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.BasicAuthAuth0Proxy
{
    public class BasicAuthAuth0ProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly BasicAuthAuth0ProxyConfig _config;
        private readonly AsyncPolicy _policy;
        private readonly IAuthenticationApiClient _auth0;

        public BasicAuthAuth0ProxyMiddleware(RequestDelegate next, BasicAuthAuth0ProxyConfig config)
        {
            _next = next;
            _config = config;

            _policy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(r));

            _auth0 = new AuthenticationApiClientCachingDecorator(new AuthenticationApiClient($"{_config.Domain}"));
        }

        public async Task Invoke(HttpContext context)
        {
            System.Net.Http.Headers.AuthenticationHeaderValue authHeader;

            if (System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Authorization"], out authHeader)
                || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["WWW-Authenticate"], out authHeader)
                || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Proxy-Authenticate"], out authHeader)
                )
            {

                if ("Basic".Equals(authHeader.Scheme,
                                     StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string parameter = Encoding.UTF8.GetString(
                                              Convert.FromBase64String(
                                                    authHeader.Parameter));

                        var parts = parameter.Split(':');

                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            string userName = parts[0];
                            string password = parts[1];

                            var accessToken = await _policy.ExecuteAsync(async (ct) =>
                                {
                                    var result = await _auth0.GetTokenAsync(new ResourceOwnerTokenRequest()
                                    {
                                        Audience = _config.Audience,
                                        ClientId = _config.ProxyClientId,
                                        Username = userName,
                                        Password = password,
                                        ClientSecret = _config.ProxySecret,
                                        Realm = _config.Realm,
                                        Scope = "openid profile email",
                                    });

                                    return result.AccessToken;
                                }, context.RequestAborted, true);

                            context.Request.Headers["Authorization"] = $@"Bearer {accessToken}";
                        }
                    }
                    catch
                    {

                    }
                }
            }

            await _next(context);
        }
    }
}
