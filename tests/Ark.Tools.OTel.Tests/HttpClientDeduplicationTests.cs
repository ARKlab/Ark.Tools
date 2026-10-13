// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.OTel.Azure;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies the HttpClient filter of the Azure setups, <see cref="Ex.AddArkAzureMonitorOpenTelemetry"/> (ASP.NET Core,
/// Distro) and <see cref="ArkAzureOpenTelemetryBuilderExtensions.AddArkAzureOpenTelemetry"/> (any host, exporter), with
/// and without Azure Monitor: HttpClient spans that duplicate an Azure SDK client span are dropped, and an application
/// filter still applies.
/// </summary>
[TestClass]
// HttpClient spans are process-wide: a parallel test's tracer provider would also collect this request.
[DoNotParallelize]
public sealed class HttpClientDeduplicationTests
{
    // Placeholder Azure Monitor target: the ingestion and live endpoints point to a closed local port.
    private const string _azureMonitorTarget =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;"
        + "IngestionEndpoint=http://127.0.0.1:1/;LiveEndpoint=http://127.0.0.1:1/";

    [TestMethod]
    [DataRow("Azure.Core.Http", false, true, true)]
    [DataRow("Ark.Tools.OTel.Tests.Dedup", true, true, true)]
    [DataRow("Azure.Core.Http", false, false, true)]
    [DataRow("Ark.Tools.OTel.Tests.Dedup", true, false, true)]
    [DataRow("Azure.Core.Http", false, true, false)]
    [DataRow("Ark.Tools.OTel.Tests.Dedup", true, true, false)]
    [DataRow("Azure.Core.Http", false, false, false)]
    [DataRow("Ark.Tools.OTel.Tests.Dedup", true, false, false)]
    public async Task AzureSetup_FiltersHttpClientSpanUnderAzureSdkSpan(
        string parentSourceName,
        bool expectHttpClientSpan,
        bool withAzureMonitor,
        bool aspNetCore)
    {
        var httpSpans = await _collectHttpClientSpans(parentSourceName, withAzureMonitor, aspNetCore, applicationFilter: null);

        httpSpans.Should().HaveCount(expectHttpClientSpan ? 1 : 0);
        AppContext.TryGetSwitch("Azure.Experimental.EnableActivitySource", out var azureSdkTracing).Should().BeTrue();
        azureSdkTracing.Should().BeTrue("Azure SDKs emit spans only with the experimental switch on");
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    public async Task AzureSetup_KeepsApplicationHttpClientFilter(bool withAzureMonitor, bool aspNetCore)
    {
        var httpSpans = await _collectHttpClientSpans(
            "Ark.Tools.OTel.Tests.Dedup.ApplicationFilter",
            withAzureMonitor,
            aspNetCore,
            static _ => false);

        httpSpans.Should().BeEmpty("UseAzureMonitor must not replace the application filter");
    }

    private async Task<IReadOnlyList<Activity>> _collectHttpClientSpans(
        string parentSourceName,
        bool withAzureMonitor,
        bool aspNetCore,
        Func<HttpRequestMessage, bool>? applicationFilter)
    {
        using var collector = new CollectingProcessor();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ApplicationInsights:ConnectionString"] = withAzureMonitor ? _azureMonitorTarget : null,
            })
            .Build();
        var services = new ServiceCollection();
        // An application filter registered before the Ark setup must still apply.
        if (applicationFilter is not null)
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
                .AddHttpClientInstrumentation(options => options.FilterHttpRequestMessage = applicationFilter));
        }

        if (aspNetCore)
            services.AddArkAzureMonitorOpenTelemetry(configuration);
        else
            services.AddOpenTelemetry().AddArkOpenTelemetry(configuration: configuration).AddArkAzureOpenTelemetry(configuration);
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(parentSourceName)
            .AddProcessor(collector));
        var serviceProvider = services.BuildServiceProvider();
        await using var serviceProviderScope = serviceProvider.ConfigureAwait(false);
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(parentSourceName);
        using var httpClient = new HttpClient();

        using var parent = source.StartActivity("parent", ActivityKind.Client);
        parent.Should().NotBeNull();
        // Nothing listens on the discard port: the request fails, but its span is still started.
        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => httpClient.GetAsync(new Uri("http://127.0.0.1:9/"), TestContext.CancellationToken)).ConfigureAwait(false);

        // The exporter's own requests to the placeholder endpoints are not children of the test span.
        return [.. collector.Spans.Where(span => span.Source.Name == "System.Net.Http" && span.ParentSpanId == parent!.SpanId)];
    }

    public TestContext TestContext { get; set; } = null!;
}
