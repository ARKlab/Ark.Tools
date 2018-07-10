using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using JWT.Builder;
using Microsoft.AspNetCore.Http;
using Polly;
using Polly.Caching;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.BasicAuthAuth0Proxy
{
    public class BasicAuthAuth0ProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly BasicAuthAuth0ProxyConfig _config;
        private readonly Policy<(string AccessToken, DateTimeOffset ExpiresOn)> _policy;
        private readonly AuthenticationApiClient _auth0;
        private readonly Polly.Caching.Memory.MemoryCacheProvider _memoryCacheProvider
   = new Polly.Caching.Memory.MemoryCacheProvider(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));


        public BasicAuthAuth0ProxyMiddleware(RequestDelegate next, BasicAuthAuth0ProxyConfig config)
        {
            _next = next;
            _config = config;
            var cachePolicy = Policy.CacheAsync(
                _memoryCacheProvider.AsyncFor<(string AccessToken, DateTimeOffset ExpiresOn)>(), 
                new ResultTtl<(string AccessToken, DateTimeOffset ExpiresOn)>(r => new Ttl(r.ExpiresOn - DateTimeOffset.Now, false)));

            var retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(r));

            _policy = Policy.WrapAsync(cachePolicy, retryPolicy.AsAsyncPolicy<(string AccessToken, DateTimeOffset ExpiresOn)>());
            _auth0 = new AuthenticationApiClient($"{_config.Domain}");
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

                        if (parts.Length == 2)
                        {
                            string userName = parts[0];
                            string password = parts[1];

                            var r = await _policy.ExecuteAsync(async (ctx, ct) =>
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

                                    var decode = new JwtBuilder()
                                        .DoNotVerifySignature()
                                        .Decode<IDictionary<string, object>>(result.AccessToken);

                                    var exp = (long)decode["exp"];

                                    return (result.AccessToken, DateTimeOffset.FromUnixTimeSeconds(exp) - TimeSpan.FromMinutes(2));
                                }, new Context(parameter), context.RequestAborted);

                            context.Request.Headers["Authorization"] = $@"Bearer {r.AccessToken}";
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
