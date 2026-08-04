// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.Tools.AspNetCore.MinimalApi.Tests;

/// <summary>Verifies that container verification gates host startup.</summary>
[TestClass]
public sealed class ArkMinimalApiHostVerificationTests
{
    private interface IMissingDependency;

    private sealed class BrokenService
    {
        public BrokenService(IMissingDependency dependency) => _ = dependency;
    }

    private sealed class WorkingService;

    private sealed class StartupProbe
    {
        public bool Started { get; set; }
    }

    [TestMethod]
    public async Task StartFailsWhenContainerVerificationFails()
    {
        await using var container = new Container();

        using var host = await CreateHostAsync(
            container,
            start: false,
            options => options.RegisterContainer = c => c.Register<BrokenService>()).ConfigureAwait(false);

        var start = async () => await host.StartAsync().ConfigureAwait(false);

        await start.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task VerificationAndStartupCallbackCompleteBeforeFirstRequest()
    {
        await using var container = new Container();
        var probe = new StartupProbe();

        using var host = await CreateHostAsync(
            container,
            start: true,
            options =>
            {
                options.RegisterContainer = c => c.RegisterInstance(probe);
                options.OnContainerVerified = c =>
                {
                    c.GetInstance<StartupProbe>().Started = true;
                };
            }).ConfigureAwait(false);

        probe.Started.Should().BeTrue();

        using var client = host.GetTestClient();
        var response = await client.GetStringAsync(new Uri("/ping", UriKind.Relative)).ConfigureAwait(false);

        response.Should().Be("pong");
    }

    private static async Task<IHost> CreateHostAsync(
        Container container,
        bool start,
        Action<ArkMinimalApiHostOptions>? configure = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services => services.AddRouting().AddArkMinimalApiHost(container, options =>
                {
                    options.RequireAuthenticatedUser = false;
                    configure?.Invoke(options);
                }));
                web.Configure(app =>
                {
                    app.UseArkMinimalApiHost(container);
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/ping", ([FromServices] StartupProbe? probe) =>
                    {
                        probe?.Started.Should().BeTrue();
                        return "pong";
                    }));
                });
            })
            .Build();

        if (start)
            await host.StartAsync().ConfigureAwait(false);

        return host;
    }
}
