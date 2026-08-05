// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

using AwesomeAssertions;

using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's classic Application Insights defaults.</summary>
[TestClass]
public sealed class ApplicationInsightsTests
{
    /// <summary>Registers the Ark initializers once and keeps the Snapshot Debugger disabled.</summary>
    [TestMethod]
    public void RegistersClassicDefaultsWithoutSnapshotDebugger()
    {
        var services = new ServiceCollection();
        services.AddArkApplicationInsightsTelemetry(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        provider.GetServices<ITelemetryInitializer>()
            .OfType<WebApiUserTelemetryInitializer>()
            .Should().ContainSingle();
        provider.GetServices<ITelemetryInitializer>()
            .OfType<WebApi4xxAsSuccessTelemetryInitializer>()
            .Should().ContainSingle();
        provider.GetService<SnapshotCollectorTelemetryModule>().Should().BeNull();
    }

    /// <summary>Marks client errors successful while retaining server-error failures.</summary>
    [TestMethod]
    public void ClassifiesClientAndServerResponses()
    {
        var services = new ServiceCollection();
        services.AddArkApplicationInsightsTelemetry(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var initializer = provider.GetServices<ITelemetryInitializer>()
            .OfType<WebApi4xxAsSuccessTelemetryInitializer>()
            .Single();

        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        var clientError = new RequestTelemetry();
        initializer.Initialize(clientError);
        clientError.Success.Should().BeTrue();

        accessor.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var serverError = new RequestTelemetry();
        initializer.Initialize(serverError);
        serverError.Success.Should().BeNull();
    }

    /// <summary>Copies the authenticated request identity to dependent telemetry.</summary>
    [TestMethod]
    public void PropagatesAuthenticatedIdentity()
    {
        var services = new ServiceCollection();
        services.AddArkApplicationInsightsTelemetry(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var initializer = provider.GetServices<ITelemetryInitializer>()
            .OfType<WebApiUserTelemetryInitializer>()
            .Single();

        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    "user-42")],
                authenticationType: "test"));
        var request = new RequestTelemetry();
        initializer.Initialize(request);

        var dependency = new DependencyTelemetry();
        dependency.Context.User.AuthenticatedUserId = request.Context.User.AuthenticatedUserId;
        dependency.Context.User.AuthenticatedUserId.Should().Be("user-42");
    }
}
