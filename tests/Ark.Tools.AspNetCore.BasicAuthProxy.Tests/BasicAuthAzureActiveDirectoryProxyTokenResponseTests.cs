// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ark.Tools.AspNetCore.BasicAuthProxy.Tests;

[TestClass]
public class BasicAuthAzureActiveDirectoryProxyTokenResponseTests
{
    // Shape of the v1 /oauth2/token response: snake_case names and numbers sent as strings.
    private const string _azureAdResponse = """
        {
            "token_type": "Bearer",
            "scope": "openid",
            "expires_in": "3599",
            "ext_expires_in": "3599",
            "expires_on": "1760536800",
            "not_before": "1760533200",
            "resource": "https://resource.example.com",
            "access_token": "test-access-token"
        }
        """;

    [TestMethod]
    public async Task Invoke_AzureAdTokenResponse_ForwardsAccessTokenAsBearer()
    {
        using var handler = new TokenHandler();
        var config = new BasicAuthAzureActiveDirectoryProxyConfig("tenant", "resource", "client", "proxy-secret");
        string? forwarded = null;
        using var sut = new BasicAuthAzureActiveDirectoryProxyMiddleware(
            ctx =>
            {
                forwarded = ctx.Request.Headers.Authorization;
                return Task.CompletedTask;
            },
            config,
            NullLogger<BasicAuthAzureActiveDirectoryProxyMiddleware>.Instance,
            handler);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Customer A:password"));

        await sut.Invoke(context).ConfigureAwait(false);

        forwarded.Should().Be("Bearer test-access-token");
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_azureAdResponse) };
        }
    }
}
