// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

using Polly;

using System.Net.Http;
using System.Text;

namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy;

public sealed class BasicAuthAzureActiveDirectoryProxyMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly BasicAuthAzureActiveDirectoryProxyConfig _config;
    private readonly HttpClient _client;

    public BasicAuthAzureActiveDirectoryProxyMiddleware(RequestDelegate next, BasicAuthAzureActiveDirectoryProxyConfig config)
    {
        _next = next;
        _config = config;
#pragma warning disable CA2000 // Dispose objects before losing scope
        _client = new HttpClient(new HttpClientHandler()
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            CheckCertificateRevocationList = true,
        });
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    public void Dispose()
    {
        ((IDisposable)_client).Dispose();
    }

    public async Task Invoke(HttpContext context)
    {
        System.Net.Http.Headers.AuthenticationHeaderValue? authHeader;

        if (System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Authorization"], out authHeader)
            || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["WWW-Authenticate"], out authHeader)
            || System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers["Proxy-Authenticate"], out authHeader)
            )
        {

            if ("Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    string parameter = Encoding.UTF8.GetString(
                                          Convert.FromBase64String(
                                                authHeader.Parameter ?? string.Empty));

                    var parts = parameter.Split(':');

                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        string username = parts[0];
                        string password = parts[1];

                        using var content = new FormUrlEncodedContent(
                            [
                                new KeyValuePair<string, string>("resource", _config.Resource ?? string.Empty),
                                new KeyValuePair<string, string>("client_id", _config.ProxyClientId ?? string.Empty),
                                new KeyValuePair<string, string>("grant_type", "password"),
                                new KeyValuePair<string, string>("username", username),
                                new KeyValuePair<string, string>("password", password),
                                new KeyValuePair<string, string>("scope", "openid"),
                                new KeyValuePair<string, string>("client_secret", _config.ProxyClientSecret ?? string.Empty),
                            ]);

                        var url = new Uri($"https://login.microsoftonline.com/{_config.Tenant}/oauth2/token");

                        var result = await Policy
                            .Handle<Exception>()
                            .RetryAsync(2)
                            .ExecuteAsync(async ct =>
                            {
                                using var res = await _client.PostAsync(url, content, context.RequestAborted).ConfigureAwait(false);
                                res.EnsureSuccessStatusCode();

                                var payload = await res.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
                                return JsonConvert.DeserializeObject<OAuthResult>(payload);
                            }, context.RequestAborted, true).ConfigureAwait(false);

                        context.Request.Headers["Authorization"] = $"Bearer {result?.Access_Token}";
                    }
                }
                catch (Exception)
                { }
#pragma warning restore CA1031 // Do not catch general exception types
            }

        }

        await _next(context).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by deserializer")]
    sealed record OAuthResult
    {
        public string? Token_Type { get; set; }
        public string? Scope { get; set; }
        public int Expires_In { get; set; }
        public int Ext_Expires_In { get; set; }
        public int Expires_On { get; set; }
        public int Not_Before { get; set; }
        public Uri? Resource { get; set; }
        public string? Access_Token { get; set; }
    }
}