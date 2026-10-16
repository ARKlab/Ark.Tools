// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.OTel.Azure;

using Azure.Monitor.OpenTelemetry.Exporter;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that <see cref="ArkAzureOpenTelemetryBuilderExtensions.AddArkAzureOpenTelemetry"/> shows server 4xx
/// requests left <see cref="ActivityStatusCode.Unset"/> as successful in Azure Monitor.
/// </summary>
[TestClass]
public sealed class AzureMonitorClientErrorsAsSuccessTests
{
    // Placeholder Azure Monitor target: the ingestion and live endpoints point to a closed local port.
    private const string _azureMonitorTarget =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;"
        + "IngestionEndpoint=http://127.0.0.1:1/;LiveEndpoint=http://127.0.0.1:1/";

    private const string _expectedTag = "test.expected";

    [TestMethod]
    [DataRow(ActivityKind.Server, 400, ActivityStatusCode.Unset, ActivityStatusCode.Ok)]
    [DataRow(ActivityKind.Server, 404, ActivityStatusCode.Unset, ActivityStatusCode.Ok)]
    [DataRow(ActivityKind.Server, 499, ActivityStatusCode.Unset, ActivityStatusCode.Ok)]
    [DataRow(ActivityKind.Server, 404L, ActivityStatusCode.Unset, ActivityStatusCode.Ok)]
    [DataRow(ActivityKind.Server, "404", ActivityStatusCode.Unset, ActivityStatusCode.Ok)]
    // The producer decided.
    [DataRow(ActivityKind.Server, 404, ActivityStatusCode.Error, ActivityStatusCode.Error)]
    [DataRow(ActivityKind.Server, "404", ActivityStatusCode.Error, ActivityStatusCode.Error)]
    [DataRow(ActivityKind.Server, 404, ActivityStatusCode.Ok, ActivityStatusCode.Ok)]
    // Other codes and kinds.
    [DataRow(ActivityKind.Server, 200, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, 399, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, 302L, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, 500, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, "500", ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, "not a code", ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Server, null, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Client, 404, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    [DataRow(ActivityKind.Client, 404, ActivityStatusCode.Error, ActivityStatusCode.Error)]
    [DataRow(ActivityKind.Internal, 404, ActivityStatusCode.Unset, ActivityStatusCode.Unset)]
    public void OnEnd_SetsOkOnlyOnUnsetServer4xx(
        ActivityKind kind,
        object? statusCode,
        ActivityStatusCode status,
        ActivityStatusCode expected)
    {
        using var processor = new AzureMonitorClientErrorsAsSuccessProcessor();
        using var source = new ActivitySource(Guid.NewGuid().ToString());
        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource == source,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("span", kind)!;
        if (statusCode is not null)
            activity.SetTag("http.response.status_code", statusCode);
        activity.SetStatus(status);

        processor.OnEnd(activity);

        activity.Status.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArkAzureOpenTelemetry_RunsProcessorAfterArkChainAndBeforeExporterProcessors()
    {
        var sourceName = nameof(AddArkAzureOpenTelemetry_RunsProcessorAfterArkChainAndBeforeExporterProcessors);
        await using var host = await _startAsync(sourceName, clientErrorsAsSuccess: true);

        var processors = _flatten(_getProcessor(host.TracerProvider)).Select(static p => p.GetType().Name).ToList();

        var order = new[]
        {
            nameof(ApplicationStatusProcessor),
            nameof(ArkFailurePromotionProcessor),
            nameof(AzureMonitorClientErrorsAsSuccessProcessor),
            "LiveMetricsActivityProcessor",
            "StandardMetricsExtractionProcessor",
            "AzureMonitorBatchActivityExportProcessor",
        };
        order.Select(p => processors.IndexOf(p)).Should().BeInAscendingOrder().And.NotContain(-1, string.Join(", ", processors));
        processors.Should().ContainSingle(static p => p == nameof(AzureMonitorClientErrorsAsSuccessProcessor));
    }

    [TestMethod]
    [DoNotParallelize] // The standard metric meter is shared by every exporter in the process.
    // Unset 4xx: shown as a success unless the application opts out.
    [DataRow(true, false, false, ActivityStatusCode.Ok, true)]
    [DataRow(false, false, false, ActivityStatusCode.Unset, false)]
    // Error 4xx: stays failed, and failure promotion keeps it.
    [DataRow(true, true, false, ActivityStatusCode.Error, false)]
    // An application processor registered before the Ark setup clears the Error first.
    [DataRow(true, true, true, ActivityStatusCode.Ok, true)]
    public async Task AddArkAzureOpenTelemetry_Server4xx_ReachesTheExporterWithTheExpectedSuccess(
        bool clientErrorsAsSuccess,
        bool instrumentationSetsError,
        bool applicationClearsError,
        ActivityStatusCode expectedStatus,
        bool expectedSuccess)
    {
        var sourceName = $"{nameof(AzureMonitorClientErrorsAsSuccessTests)}.{clientErrorsAsSuccess}.{instrumentationSetsError}.{applicationClearsError}";
        var standardMetricSuccess = new List<object?>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == "StandardMetricMeter" && instrument.Name == "RequestDurationStandardMetric")
                    listener.EnableMeasurementEvents(instrument);
            },
        };
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "Request.Success")
                    standardMetricSuccess.Add(tag.Value);
            }
        });
        meterListener.Start();
        await using var host = await _startAsync(sourceName, clientErrorsAsSuccess);
        using var source = new ActivitySource(sourceName);

        var activity = source.StartActivity("GET /orders/{id}", ActivityKind.Server);
        activity.Should().NotBeNull();
        activity!.Recorded.Should().BeFalse("the exhausted adaptive budget makes the request RecordOnly");
        // The tags of an ASP.NET Core request: the exporter applies its HTTP rule only to HTTP spans.
        activity.SetTag("http.request.method", "GET");
        activity.SetTag("url.scheme", "https");
        activity.SetTag("url.path", "/orders/42");
        activity.SetTag("http.route", "/orders/{id}");
        activity.SetTag("http.response.status_code", 400);
        if (instrumentationSetsError)
            activity.SetStatus(ActivityStatusCode.Error);
        if (applicationClearsError)
            activity.SetTag(_expectedTag, true);
        activity.Stop();

        activity.Status.Should().Be(expectedStatus);
        standardMetricSuccess.Should().Equal(expectedSuccess);
        _toRequestDataSuccess(activity).Should().Be(expectedSuccess);
        // Unset and Ok spans are not failures: only the Error span is promoted.
        activity.Recorded.Should().Be(expectedStatus == ActivityStatusCode.Error);
    }

    private static async Task<StartedHost> _startAsync(string sourceName, bool clientErrorsAsSuccess)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ApplicationInsights:ConnectionString"] = _azureMonitorTarget,
                ["OTel:ArkAdaptiveSampler:TracesPerSecond"] = "0.0001",
                ["OTel:ArkAdaptiveSampler:EnablePerOperationBucketing"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        // Registered before the Ark setup, so it runs before the Ark chain.
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(sourceName)
            .AddProcessor(new ApplicationStatusProcessor()));
        services.AddOpenTelemetry()
            .AddArkAspNetCoreOpenTelemetry(configuration: configuration)
            .AddArkAzureOpenTelemetry(configuration, clientErrorsAsSuccess);
        services.Configure<AzureMonitorExporterOptions>(static options => options.DisableOfflineStorage = true);
        var serviceProvider = services.BuildServiceProvider();

        // The exporter adds its trace processors from a hosted service, when the host starts.
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);

        return new StartedHost(serviceProvider, hostedServices);
    }

    private static BaseProcessor<Activity> _getProcessor(TracerProvider tracerProvider)
    {
        // The SDK does not expose the processor chain publicly.
        return (BaseProcessor<Activity>)tracerProvider.GetType()
            .GetProperty("Processor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tracerProvider)!;
    }

    private static IEnumerable<BaseProcessor<Activity>> _flatten(BaseProcessor<Activity> processor)
    {
        if (processor is not CompositeProcessor<Activity> composite)
            return [processor];

        var children = (IReadOnlyList<BaseProcessor<Activity>>)typeof(CompositeProcessor<Activity>)
            .GetMethod("ToReadOnlyList", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(composite, null)!;
        return children.SelectMany(_flatten);
    }

    private static bool _toRequestDataSuccess(Activity activity)
    {
        // The exporter's own conversion to the Application Insights request item.
        var traceHelper = typeof(AzureMonitorExporterOptions).Assembly
            .GetType("Azure.Monitor.OpenTelemetry.Exporter.Internals.TraceHelper", throwOnError: true)!;
        var result = (ITuple)traceHelper
            .GetMethod("OtelToAzureMonitorTrace", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new Batch<Activity>([activity], 1), null, "00000000-0000-0000-0000-000000000000", 100f])!;
        var telemetryItem = ((System.Collections.IEnumerable)result[0]!).Cast<object>().Single();
        var monitorBase = telemetryItem.GetType().GetProperty("Data")!.GetValue(telemetryItem)!;
        var requestData = monitorBase.GetType().GetProperty("BaseData")!.GetValue(monitorBase)!;
        requestData.GetType().Name.Should().Be("RequestData");
        return (bool)requestData.GetType().GetProperty("Success")!.GetValue(requestData)!;
    }

    private sealed class StartedHost(ServiceProvider serviceProvider, List<IHostedService> hostedServices) : IAsyncDisposable
    {
        public TracerProvider TracerProvider { get; } = serviceProvider.GetRequiredService<TracerProvider>();

        public async ValueTask DisposeAsync()
        {
            foreach (var hostedService in hostedServices)
                await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Clears the Error of an expected server 4xx, as an application processor registered before the Ark setup can.
    private sealed class ApplicationStatusProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.GetTagItem(_expectedTag) is true)
                data.SetStatus(ActivityStatusCode.Unset);
        }
    }
}
