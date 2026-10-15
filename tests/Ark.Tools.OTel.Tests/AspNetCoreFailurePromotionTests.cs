// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies failure promotion through the processors registered by
/// <see cref="Ex.AddArkAspNetCoreOpenTelemetry"/>.
/// </summary>
[TestClass]
public sealed class AspNetCoreFailurePromotionTests
{
    [TestMethod]
    // Server 4xx: the producer decides; Ark keeps its status.
    [DataRow(ActivityKind.Server, 404, false, false, false, false, ActivityStatusCode.Unset, false)]
    [DataRow(ActivityKind.Server, 404, true, false, false, false, ActivityStatusCode.Error, true)]
    // The opt-in WebApi4xxAsSuccessProcessor still clears it when the application registers it.
    [DataRow(ActivityKind.Server, 404, true, false, false, true, ActivityStatusCode.Unset, false)]
    [DataRow(ActivityKind.Server, 500, true, false, false, false, ActivityStatusCode.Error, true)]
    [DataRow(ActivityKind.Client, 404, true, false, false, false, ActivityStatusCode.Error, true)]
    [DataRow(ActivityKind.Client, 404, true, true, false, false, ActivityStatusCode.Unset, false)]
    // An exception event without the Error status is not a failure: the producer decides.
    [DataRow(ActivityKind.Internal, 0, false, false, true, false, ActivityStatusCode.Unset, false)]
    [DataRow(ActivityKind.Internal, 0, true, false, true, false, ActivityStatusCode.Error, true)]
    public void AddArkAspNetCoreOpenTelemetry_PromotesOnlyFailures(
        ActivityKind kind,
        int httpStatus,
        bool instrumentationSetsError,
        bool applicationClearsStatus,
        bool recordException,
        bool registerWebApi4xxAsSuccessProcessor,
        ActivityStatusCode expectedStatus,
        bool expectedPromoted)
    {
        var sourceName = $"{nameof(AspNetCoreFailurePromotionTests)}.{kind}.{httpStatus}.{instrumentationSetsError}.{applicationClearsStatus}.{recordException}.{registerWebApi4xxAsSuccessProcessor}";
        using var collector = new CollectingProcessor();
        var services = new ServiceCollection();
        // An application processor registered before Ark's processors runs before failure promotion.
        services.ConfigureOpenTelemetryTracerProvider(tracing =>
        {
            tracing.AddSource(sourceName).AddProcessor(new ExpectedDependencyStatusProcessor());
#pragma warning disable CS0618 // Obsolete: the opt-in must keep working for the applications that register it.
            if (registerWebApi4xxAsSuccessProcessor)
                tracing.AddProcessor(new WebApi4xxAsSuccessProcessor());
#pragma warning restore CS0618
        });
        services.AddOpenTelemetry().AddArkAspNetCoreOpenTelemetry(configureAdaptiveSampler: static options =>
        {
            options.TracesPerSecond = 0.0001;
            options.EnablePerOperationBucketing = false;
        });
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(collector));
        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(sourceName);

        var activity = source.StartActivity("span", kind);
        activity.Should().NotBeNull();
        activity!.Recorded.Should().BeFalse("the exhausted adaptive budget makes the span RecordOnly");
        if (httpStatus != 0)
            activity.SetTag("http.response.status_code", httpStatus);

        // Simulates the instrumentation or the application marking the span as failed.
        if (instrumentationSetsError)
            activity.SetStatus(ActivityStatusCode.Error);

        if (applicationClearsStatus)
            activity.SetTag(ExpectedDependencyStatusProcessor.ExpectedTag, true);

        if (recordException)
            activity.AddException(new InvalidOperationException("boom"));

        activity.Stop();

        activity.Status.Should().Be(expectedStatus);
        collector.Spans.Contains(activity).Should().Be(expectedPromoted);
    }

    private sealed class ExpectedDependencyStatusProcessor : BaseProcessor<Activity>
    {
        public const string ExpectedTag = "test.expected";

        public override void OnEnd(Activity data)
        {
            if (data.Kind == ActivityKind.Client && data.GetTagItem(ExpectedTag) is true)
                data.SetStatus(ActivityStatusCode.Unset);
        }
    }
}
