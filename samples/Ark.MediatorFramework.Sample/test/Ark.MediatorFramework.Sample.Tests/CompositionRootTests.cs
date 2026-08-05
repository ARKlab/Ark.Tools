// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.WebInterface;
using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.MinimalApi;

using AwesomeAssertions;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's production composition root in an isolated host.</summary>
[TestClass]
public sealed class CompositionRootTests
{
    /// <summary>Runs production registrations without contacting external providers.</summary>
    [TestMethod]
    public async Task ProductionCompositionStartsAndExposesHealth()
    {
        var network = new InMemNetwork();
        var store = new InMemoryGreetingStore();
        var container = SampleComposition.BuildContainer(
            network,
            useSqlStore: false,
            greetingStore: store);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = "IntegrationTests",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();

        var startup = SampleHost.Configure(
            builder,
            container,
            network,
            useSqlStore: false,
            sharedStore: store);
        await using var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync().ConfigureAwait(false);

        app.Services.GetService<ArkMinimalApiHostOptions>().Should().NotBeNull();
        app.Services.GetService<IHealthCheckService>().Should().NotBeNull();
        app.Services.GetServices<ITelemetryInitializer>()
            .Should().Contain(item => item is WebApiUserTelemetryInitializer);

        using var response = await app.GetTestClient().GetAsync("/healthCheck").ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
