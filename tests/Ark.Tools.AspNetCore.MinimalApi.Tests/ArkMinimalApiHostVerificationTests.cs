// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
    public async Task VerificationCompletesBeforeFirstRequest()
    {
        await using var container = new Container();
        var verified = false;

        using var host = await CreateHostAsync(
            container,
            start: true,
            options =>
            {
                options.RegisterContainer = c => c.Register<WorkingService>();
                options.OnContainerVerified = _ => verified = true;
            }).ConfigureAwait(false);

        verified.Should().BeTrue();

        using var client = host.GetTestClient();
        var response = await client.GetAsync(new Uri("/ping", UriKind.Relative)).ConfigureAwait(false);

        response.IsSuccessStatusCode.Should().BeTrue();
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
                    app.UseEndpoints(endpoints => endpoints.MapGet("/ping", () => "pong"));
                });
            })
            .Build();

        if (start)
            await host.StartAsync().ConfigureAwait(false);

        return host;
    }
}
