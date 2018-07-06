using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ark.AspNetCore.BasicAuthProxy
{
    public class BasicAuthAzureActiveDirectoryProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly BasicAuthAzureActiveDirectoryProxyConfig _config;
        private readonly HttpClient _client;

        public BasicAuthAzureActiveDirectoryProxyMiddleware(RequestDelegate next, BasicAuthAzureActiveDirectoryProxyConfig config)
        {
            _next = next;
            _config = config;
            _client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip
            });
        }

        public async Task Invoke(HttpContext context)
        {
            System.Net.Http.Headers.AuthenticationHeaderValue authHeader;

            if (System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Authorization"], out authHeader)
                || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["WWW-Authenticate"], out authHeader)
                || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Proxy-Authenticate"], out authHeader)
                )
            {

                if ("Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string parameter = Encoding.UTF8.GetString(
                                              Convert.FromBase64String(
                                                    authHeader.Parameter));

                        var parts = parameter.Split(':');

                        if (parts.Length == 2)
                        {
                            string username = parts[0];
                            string password = parts[1];

                            var content = new FormUrlEncodedContent(new[]
                                {
                                    new KeyValuePair<string, string>("resource", _config.Resource),
                                    new KeyValuePair<string, string>("client_id", _config.ProxyClientId),
                                    new KeyValuePair<string, string>("grant_type", "password"),
                                    new KeyValuePair<string, string>("username", username),
                                    new KeyValuePair<string, string>("password", password),
                                    new KeyValuePair<string, string>("scope", "openid"),
                                    new KeyValuePair<string, string>("client_secret", _config.ProxyClientSecret),
                                });

                            var url = $"https://login.microsoftonline.com/{_config.Tenant}/oauth2/token";

                            var result = await Policy
                                .Handle<Exception>()
                                .RetryAsync(2)
                                .ExecuteAsync(async () => {
                                    var res = await _client.PostAsync(url, content);
                                    res.EnsureSuccessStatusCode();

                                    var payload = await res.Content.ReadAsStringAsync();
                                    return JsonConvert.DeserializeObject<OAuthResult>(payload);
                                });

                            context.Request.Headers["Authorization"] = $"Bearer {result.Access_Token}";
                        }
                    }
                    catch (Exception)
                    { }
                }

            }

            await _next(context);
        }

        class OAuthResult
        {
            public string Token_Type { get; set; }
            public string Scope { get; set; }
            public int Expires_In { get; set; }
            public int Ext_Expires_In { get; set; }
            public int Expires_On { get; set; }
            public int Not_Before { get; set; }
            public Uri Resource { get; set; }
            public string Access_Token { get; set; }
        }
    }
}
