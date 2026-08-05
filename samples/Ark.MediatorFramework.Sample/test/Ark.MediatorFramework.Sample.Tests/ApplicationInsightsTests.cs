// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

using AwesomeAssertions;

using Microsoft.ApplicationInsights.DataContracts;
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
        services.ArkApplicationInsightsTelemetry(new ConfigurationBuilder().Build());

        services.Count(descriptor => descriptor.ImplementationType == typeof(WebApiUserTelemetryInitializer))
            .Should().Be(1);
        services.Count(descriptor => descriptor.ImplementationType == typeof(WebApi4xxAsSuccessTelemetryInitializer))
            .Should().Be(1);
        services.Any(descriptor =>
            descriptor.ServiceType.FullName is { } fullName
            && fullName.Contains("SnapshotCollector", StringComparison.Ordinal))
            .Should().BeFalse();
    }

    /// <summary>Marks client errors successful while retaining server-error failures.</summary>
    [TestMethod]
    public void ClassifiesClientAndServerResponses()
    {
        var accessor = new HttpContextAccessor();
        var initializer = new WebApi4xxAsSuccessTelemetryInitializer(accessor);

        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        var clientError = new RequestTelemetry();
        accessor.HttpContext.Features.Set(clientError);
        initializer.Initialize(clientError);
        clientError.Success.Should().BeTrue();

        accessor.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var serverError = new RequestTelemetry();
        accessor.HttpContext.Features.Set(serverError);
        initializer.Initialize(serverError);
        serverError.Success.Should().NotBeTrue();
    }

    /// <summary>Copies the authenticated request identity to dependent telemetry.</summary>
    [TestMethod]
    public void PropagatesAuthenticatedIdentity()
    {
        var accessor = new HttpContextAccessor();
        var initializer = new WebApiUserTelemetryInitializer(accessor);

        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    "user-42")],
                authenticationType: "test"));
        var request = new RequestTelemetry();
        accessor.HttpContext.Features.Set(request);
        initializer.Initialize(request);

        var dependency = new DependencyTelemetry();
        initializer.Initialize(dependency);
        dependency.Context.User.AuthenticatedUserId.Should().Be("user-42");
    }
}
