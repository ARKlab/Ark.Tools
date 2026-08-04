// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle("max-age=31536000");
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
                });
                web.Configure(app =>
                {
                    app.UseArkMinimalApiSecurity();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/", () => "ok"));
                });
            })
            .Build();

        await host.StartAsync().ConfigureAwait(false);
        return host;
    }
}
