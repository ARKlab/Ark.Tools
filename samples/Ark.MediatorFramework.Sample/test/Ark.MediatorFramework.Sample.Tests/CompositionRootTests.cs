// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.


using Ark.Tools.Outbox;
using Ark.MediatorFramework.Sample.WebInterface;
using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.MinimalApi;

using AwesomeAssertions;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NLog;

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
        var dataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var container = SampleComposition.BuildContainer(
            network,
            useSqlStore: false,
            dataContextFactory: dataContextFactory);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SampleHost).Assembly.GetName().Name,
            EnvironmentName = "IntegrationTests",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();

        var startup = SampleHost.Configure(
            builder,
            container,
            network,
            useSqlStore: false,
            sharedDataContextFactory: dataContextFactory);
        await using var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        app.Services.GetService<ArkMinimalApiHostOptions>().Should().NotBeNull();
        app.Services.GetService<HealthCheckService>().Should().NotBeNull();
        app.Services.GetServices<ITelemetryInitializer>()
            .Should().Contain(item => item is WebApiUserTelemetryInitializer);
        container.IsLocked.Should().BeTrue();
        LogManager.Configuration.Should().NotBeNull();

        using var response = await app.GetTestClient().GetAsync(
            new Uri("/healthCheck", UriKind.Relative),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
