// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

using System.Net;

namespace Ark.Tools.AspNetCore.MinimalApi.Tests;

/// <summary>Verifies which X-Forwarded-Prefix values the Ark Minimal API host accepts.</summary>
[TestClass]
public sealed class ArkMinimalApiForwardedPrefixTests
{
    [TestMethod]
    [DataRow("/api")]
    [DataRow("/api/v1")]
    [DataRow("/a%41")]
    [DataRow("/a%2e%2e")]
    [DataRow("/ok%25")]
    public async Task ValidPrefix_IsAppliedToPathBase(string prefix)
    {
        using var response = await _sendAsync(prefix).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Be(prefix);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("api")]
    [DataRow("//api")]
    [DataRow("/api/")]
    [DataRow("/a//b")]
    [DataRow("/./a")]
    [DataRow("/a/..")]
    [DataRow("/a,b")]
    [DataRow("/a\\b")]
    [DataRow("/a?b")]
    [DataRow("/a#b")]
    [DataRow("/a:b")]
    [DataRow("/a b")]
    [DataRow("/a%4")]
    [DataRow("/a%zz")]
    [DataRow("/%2e")]
    [DataRow("/%2e%2E")]
    [DataRow("/a%2Fb")]
    [DataRow("/a%5Cb")]
    [DataRow("/a%20b")]
    [DataRow("/a%0Ab")]
    [DataRow("/a%7F")]
    public async Task InvalidPrefix_IsRejected(string prefix)
    {
        using var response = await _sendAsync(prefix).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> _sendAsync(string prefix)
    {
        using var container = new Container();
        using var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services => services.AddRouting().AddArkMinimalApiHost(container));
                web.Configure(app =>
                {
                    app.UseArkMinimalApiHost(container);
                    app.UseEndpoints(static endpoints => endpoints
                        .MapGet("/echo", static (HttpContext context) => context.Request.PathBase.Value)
                        .AllowAnonymous());
                });
            })
            .Build();
        await host.StartAsync().ConfigureAwait(false);

        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/echo", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", prefix).Should().BeTrue();
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
