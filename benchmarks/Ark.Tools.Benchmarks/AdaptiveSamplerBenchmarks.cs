// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

using Ark.Tools.OTel;

using BenchmarkDotNet.Attributes;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Ark.Tools.Benchmarks;

/// <summary>
/// Per-span costs of <see cref="ArkAdaptiveSampler"/> and <see cref="ArkFailurePromotionProcessor"/>
/// through a real tracer provider, including the SDK copying sampling attributes to the span.
/// </summary>
[Config(typeof(HotPathBenchmarks.BenchmarkConfig))]
[MemoryDiagnoser]
public class AdaptiveSamplerBenchmarks
{
    private const string _sampledSource = "Ark.Benchmarks.Sampled";
    private const string _recordOnlySource = "Ark.Benchmarks.RecordOnly";

    private static readonly ActivitySource _sampled = new(_sampledSource);
    private static readonly ActivitySource _recordOnly = new(_recordOnlySource);
    private TracerProvider? _sampledProvider;
    private TracerProvider? _recordOnlyProvider;

    /// <summary>Builds one pipeline that samples every root and one that records them only.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _sampledProvider = _build(_sampledSource, tracesPerSecond: 1_000_000_000);
        _recordOnlyProvider = _build(_recordOnlySource, tracesPerSecond: 0.0001);
    }

    /// <summary>Disposes the pipelines.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _sampledProvider?.Dispose();
        _recordOnlyProvider?.Dispose();
    }

    /// <summary>Starts and stops a root the budget samples.</summary>
    [Benchmark(Baseline = true)]
    public void SampledRoot()
    {
        using var root = _sampled.StartActivity("GET /books", ActivityKind.Server);
    }

    /// <summary>Starts and stops a sampled root with three children, as a request with dependencies.</summary>
    [Benchmark]
    public void SampledRootWithThreeChildren()
    {
        using var root = _sampled.StartActivity("GET /books", ActivityKind.Server);
        for (var i = 0; i < 3; i++)
        {
            using var child = _sampled.StartActivity("SELECT", ActivityKind.Client);
        }
    }

    /// <summary>Starts and stops a root the exhausted budget records only.</summary>
    [Benchmark]
    public void RecordOnlyRoot()
    {
        using var root = _recordOnly.StartActivity("GET /books", ActivityKind.Server);
    }

    /// <summary>Starts and stops a record-only root with three children.</summary>
    [Benchmark]
    public void RecordOnlyRootWithThreeChildren()
    {
        using var root = _recordOnly.StartActivity("GET /books", ActivityKind.Server);
        for (var i = 0; i < 3; i++)
        {
            using var child = _recordOnly.StartActivity("SELECT", ActivityKind.Client);
        }
    }

    private static TracerProvider _build(string sourceName, double tracesPerSecond)
    {
        var registry = new FailedTraceRegistry();
        var options = new ArkAdaptiveSamplerOptions { TracesPerSecond = tracesPerSecond };
#pragma warning disable CA2000 // The tracer provider owns and disposes the processor.
        return Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new ArkAdaptiveSampler(options, registry))
            .AddProcessor(new ArkFailurePromotionProcessor(registry))
            .Build()!;
#pragma warning restore CA2000
    }
}
