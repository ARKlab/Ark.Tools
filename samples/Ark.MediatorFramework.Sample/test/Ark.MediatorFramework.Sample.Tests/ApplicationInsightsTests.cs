// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's OpenTelemetry Application Insights defaults.</summary>
[TestClass]
public sealed class ApplicationInsightsTests
{
    /// <summary>Registers the Ark OpenTelemetry customization without the Snapshot Debugger.</summary>
    [TestMethod]
    public void RegistersOpenTelemetryDefaultsWithoutSnapshotDebugger()
    {
        var services = new ServiceCollection();
        services.ArkApplicationInsightsTelemetry(new ConfigurationBuilder().Build());

        services.Any(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TelemetryConfiguration>))
            .Should().BeTrue();
        services.Any(descriptor =>
            descriptor.ServiceType.FullName is { } fullName
            && fullName.Contains("SnapshotCollector", StringComparison.Ordinal))
            .Should().BeFalse();
    }

    /// <summary>Marks client errors successful only on server request spans.</summary>
    [TestMethod]
    public void ClassifiesClientAndServerSpans()
    {
        using var processor = new WebApi4xxAsSuccessProcessor();
        using var source = new ActivitySource(Guid.NewGuid().ToString());
        using var listener = _createListener(source);
        using var request = _createActivity(source, ActivityKind.Server, 404);
        request.SetStatus(ActivityStatusCode.Error);
        processor.OnEnd(request);
        request.Status.Should().Be(ActivityStatusCode.Unset);

        using var stringRequest = _createActivity(source, ActivityKind.Server, "404");
        stringRequest.SetStatus(ActivityStatusCode.Error);
        processor.OnEnd(stringRequest);
        stringRequest.Status.Should().Be(ActivityStatusCode.Unset);

        using var dependency = _createActivity(source, ActivityKind.Client, 404);
        dependency.SetStatus(ActivityStatusCode.Error);
        processor.OnEnd(dependency);
        dependency.Status.Should().Be(ActivityStatusCode.Error);
    }

    /// <summary>Adds the stable authenticated identifier only to server request spans.</summary>
    [TestMethod]
    public void EnrichesServerSpanWithAuthenticatedIdentity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "private-name"),
                new Claim(ClaimTypes.NameIdentifier, "user-42")
            ],
            authenticationType: "test"));
        using var processor = new WebApiUserProcessor(new FixedUserContext(principal));
        using var source = new ActivitySource(Guid.NewGuid().ToString());
        using var listener = _createListener(source);
        using var request = _createActivity(source, ActivityKind.Server);

        processor.OnEnd(request);

        request.GetTagItem("enduser.id").Should().Be("user-42");

        using var dependency = _createActivity(source, ActivityKind.Client);
        processor.OnEnd(dependency);
        dependency.GetTagItem("enduser.id").Should().BeNull();
    }

    private static ActivityListener _createListener(ActivitySource source)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static Activity _createActivity(ActivitySource source, ActivityKind kind, object? statusCode = null)
    {
        var activity = source.StartActivity("test", kind)!;
        if (statusCode is not null)
            activity.SetTag("http.response.status_code", statusCode);

        return activity;
    }

    private sealed class FixedUserContext(ClaimsPrincipal principal) : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current { get; } = principal;
    }
}
