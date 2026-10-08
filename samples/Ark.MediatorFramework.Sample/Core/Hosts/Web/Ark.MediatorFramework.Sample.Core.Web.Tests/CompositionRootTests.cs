// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NLog;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Verifies the sample's production composition root in an isolated host.</summary>
[TestClass]
public sealed class CompositionRootTests
{
    /// <summary>Exposes the SimpleInjector-backed processors through the host service provider.</summary>
    [TestMethod]
    public async Task ProductionCompositionRegistersProcessorsInMicrosoftDependencyInjection()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);

        host.App.Services.GetRequiredService<IRequestProcessor>().Should().NotBeNull();
        host.App.Services.GetRequiredService<IQueryProcessor>().Should().NotBeNull();
        host.App.Services.GetRequiredService<ICommandProcessor>().Should().NotBeNull();
    }

    /// <summary>Runs production registrations without contacting external providers.</summary>
    [TestMethod]
    public async Task ProductionCompositionStartsAndExposesHealth()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var app = host.App;

        app.Services.GetService<ArkMinimalApiHostOptions>().Should().NotBeNull();
        app.Services.GetService<HealthCheckService>().Should().NotBeNull();
        host.Container.IsLocked.Should().BeTrue();
        LogManager.Configuration.Should().NotBeNull();

        using var response = await app.GetTestClient().GetAsync(
            new Uri("/healthCheck", UriKind.Relative),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary>The api process registers no completed-print subscriber; the subscriber processes own it.</summary>
    [TestMethod]
    public async Task WebInterfaceContainerRegistersNoSubscriberHandler()
    {
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        await using var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });

        container.GetRegistration<ICommandHandler<BookPrintCompleted>>(throwOnFailure: false)
            .Should().BeNull();
    }
}
