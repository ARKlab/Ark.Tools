// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

using System.Net;

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

    [TestMethod]
    public async Task HealthChecksAreAnonymousWhenAuthenticationIsRequired()
    {
        await using var container = new Container();
        using var host = await CreateHostAsync(
            container,
            start: true,
            requireAuthenticatedUser: true).ConfigureAwait(false);

        using var client = host.GetTestClient();
        using var response = await client.GetAsync(
            new Uri("/healthCheck", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task HealthCheckReportsDependencyFailureWithoutExceptionDetails()
    {
        await using var container = new Container();
        using var host = await CreateHostAsync(
            container,
            start: true,
            configureServices: services => services
                .AddHealthChecks()
                .AddCheck("database", () => HealthCheckResult.Unhealthy("secret-connection-details",
                    new InvalidOperationException("secret-exception-details")))).ConfigureAwait(false);

        using var client = host.GetTestClient();
        using var response = await client.GetAsync(new Uri("/healthCheck", UriKind.Relative)).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().Contain("\"status\":\"Unhealthy\"");
        body.Should().Contain("\"name\":\"database\"");
        body.Should().NotContain("secret-connection-details");
        body.Should().NotContain("secret-exception-details");
        body.Should().NotContain("InvalidOperationException");
    }

    [TestMethod]
    public async Task DefaultHealthChecksDoNotRegisterUiOrHistoryServices()
    {
        await using var container = new Container();
        using var host = await CreateHostAsync(
            container,
            start: true,
            configureServices: services =>
            {
                services.Should().NotContain(service => service.ServiceType.FullName?.Contains(
                    "HealthChecks.UI", StringComparison.Ordinal) == true);
            }).ConfigureAwait(false);

        host.Should().NotBeNull();
    }

    private static async Task<IHost> CreateHostAsync(
        Container container,
        bool start,
        Action<ArkMinimalApiHostOptions>? configure = null,
        bool requireAuthenticatedUser = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting().AddArkMinimalApiHost(container, options =>
                    {
                        options.RequireAuthenticatedUser = requireAuthenticatedUser;
                        configure?.Invoke(options);
                    });
                    configureServices?.Invoke(services);
                });
                web.Configure(app =>
                {
                    app.UseArkMinimalApiHost(container);
                    app.UseEndpoints(endpoints => endpoints
                        .MapArkMinimalApiHost()
                        .MapGet("/ping", ([FromServices] StartupProbe? probe) =>
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
