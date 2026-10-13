// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/> drops HttpClient spans
/// that duplicate an Azure SDK client span.
/// </summary>
[TestClass]
// HttpClient spans are process-wide: a parallel test's tracer provider would also collect this request.
[DoNotParallelize]
public sealed class HttpClientDeduplicationTests
{
    [TestMethod]
    [DataRow("Azure.Core.Http", false)]
    [DataRow("Ark.Tools.OTel.Tests.Dedup", true)]
    public async Task AddArkOpenTelemetry_FiltersHttpClientSpanUnderAzureSdkSpan(
        string parentSourceName,
        bool expectHttpClientSpan)
    {
        using var collector = new CollectingProcessor();
        var services = new ServiceCollection();
        services.AddOpenTelemetry().AddArkOpenTelemetry();
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(parentSourceName)
            .AddProcessor(collector));
        await using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(parentSourceName);
        using var httpClient = new HttpClient();

        using (var parent = source.StartActivity("parent", ActivityKind.Client))
        {
            parent.Should().NotBeNull();
            // Nothing listens on the discard port: the request fails, but its span is still started.
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => httpClient.GetAsync(new Uri("http://127.0.0.1:9/"), TestContext.CancellationToken));
        }

        collector.Spans.Any(static span => span.Source.Name == "System.Net.Http")
            .Should().Be(expectHttpClientSpan);
    }

    [TestMethod]
    public async Task AddArkOpenTelemetry_KeepsApplicationHttpClientFilter()
    {
        const string parentSourceName = "Ark.Tools.OTel.Tests.Dedup.ApplicationFilter";
        using var collector = new CollectingProcessor();
        var services = new ServiceCollection();
        // An application filter registered before the Ark setup must still apply.
        services.ConfigureOpenTelemetryTracerProvider(static tracing => tracing
            .AddHttpClientInstrumentation(static options => options.FilterHttpRequestMessage = static _ => false));
        services.AddOpenTelemetry().AddArkOpenTelemetry();
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(parentSourceName)
            .AddProcessor(collector));
        await using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(parentSourceName);
        using var httpClient = new HttpClient();

        using (source.StartActivity("parent", ActivityKind.Client))
        {
            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => httpClient.GetAsync(new Uri("http://127.0.0.1:9/"), TestContext.CancellationToken));
        }

        collector.Spans.Should().NotContain(static span => span.Source.Name == "System.Net.Http");
    }

    public TestContext TestContext { get; set; } = null!;
}
