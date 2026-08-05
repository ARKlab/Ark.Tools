// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.MinimalApi.Tests;

/// <summary>Verifies the optional Ark Minimal API security profile.</summary>
[TestClass]
public sealed class ArkMinimalApiSecurityTests
{
    [TestMethod]
    public async Task AddsSecurityHeadersAndHsts()
    {
        using var host = await CreateHostAsync().ConfigureAwait(false);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        using var response = await client.GetAsync(new Uri("https://localhost/")).ConfigureAwait(false);

        response.Headers.Server.ToString().Should().BeEmpty();
        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
    }

    [TestMethod]
    public async Task ApiPolicy_HasSameOriginCrossOriginOpenerPolicy()
    {
        using var host = await CreateHostAsync().ConfigureAwait(false);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        using var response = await client.GetAsync(new Uri("https://localhost/")).ConfigureAwait(false);

        response.Headers.GetValues("Cross-Origin-Opener-Policy").Should().ContainSingle("same-origin");
    }

    [TestMethod]
    [DataRow("/scalar")]
    [DataRow("/scalar/v1")]
    [DataRow("/swagger")]
    [DataRow("/openapi")]
    public async Task DocumentationPolicy_HasUnsafeNoneCrossOriginOpenerPolicy(string path)
    {
        using var host = await CreateHostAsync().ConfigureAwait(false);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        using var response = await client.GetAsync(new Uri($"https://localhost{path}")).ConfigureAwait(false);

        response.Headers.GetValues("Cross-Origin-Opener-Policy").Should().ContainSingle("unsafe-none");
    }

    [TestMethod]
    [DataRow("/error", 500)]
    [DataRow("/not-found", 404)]
    public async Task SecurityHeaders_ArePresentOnErrorAndNotFound(string path, int expectedStatusCode)
    {
        using var host = await CreateHostAsync().ConfigureAwait(false);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        using var response = await client.GetAsync(new Uri($"https://localhost{path}")).ConfigureAwait(false);

        response.StatusCode.Should().Be((System.Net.HttpStatusCode)expectedStatusCode);
        response.Headers.Server.ToString().Should().BeEmpty();
        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
    }

    private static async Task<IHost> CreateHostAsync()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment(Environments.Production);
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddArkMinimalApiSecurity();
                    services.Configure<HstsOptions>(o => o.ExcludedHosts.Clear());
                });
                web.Configure(app =>
                {
                    app.Use((context, next) =>
                    {
                        context.Request.Scheme = "https";
                        return next();
                    });
                    app.UseArkMinimalApiSecurity();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/", () => "ok");
                        endpoints.MapGet("/scalar/{**path}", () => "scalar ui");
                        endpoints.MapGet("/swagger/{**path}", () => "swagger ui");
                        endpoints.MapGet("/openapi/{**path}", () => "openapi ui");
                        endpoints.MapGet("/error", () => Results.StatusCode(500));
                    });
                });
            })
            .Build();

        await host.StartAsync().ConfigureAwait(false);
        return host;
    }
}
