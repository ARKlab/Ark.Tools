// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;
using System.Diagnostics;
using System.Text.Json;

using Ark.Tools.Core.Reflection;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.OTel;
using Ark.Tools.SystemTextJson;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using Microsoft.AspNetCore.Http;

using OpenTelemetry.Trace;

namespace Ark.Tools.Benchmarks;

/// <summary>Per-call costs of library code that runs per span, request, payload or row.</summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RequiresUnreferencedCode("Benchmarks reflection-based polymorphic paths.")]
public class HotPathBenchmarks
{
    private const int _parallelCalls = 4_096;
    private const string _labelledQuery = "SET NOCOUNT ON;\r\nSELECT [Id], [Name]\r\nFROM [dbo].[Book]\r\n-- otel-query-label: GetBooks\r\nWHERE [Id] = @Id";
    private const string _polymorphicJson = """{"kind":"Circle","radius":2.5,"name":"wheel"}""";

    private readonly ArkAdaptiveSampler _sampler = new(new ArkAdaptiveSamplerOptions { TracesPerSecond = 1_000_000 });
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, Converters = { new ShapeConverter() } };
    private readonly DefaultHttpContext _httpContext = new();
    private Shape[] _rows = [];

    /// <summary>Creates the polymorphic rows.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rows = Enumerable.Range(0, 1_000)
            .Select(static i => i % 2 == 0 ? (Shape)new Circle { Name = "c", Radius = i } : new Square { Name = "s", Side = i })
            .ToArray();
    }

    /// <summary>Samples one root span (no parent) through per-operation buckets.</summary>
    /// <returns>The sampling decision.</returns>
    [Benchmark]
    public SamplingDecision SamplerRootSpan()
    {
        var parameters = new SamplingParameters(default, ActivityTraceId.CreateRandom(), "GET /books", ActivityKind.Server);
        return _sampler.ShouldSample(parameters).Decision;
    }

    /// <summary>Samples root spans from four threads at once, where bucket lookup contention shows.</summary>
    [Benchmark(OperationsPerInvoke = _parallelCalls)]
    public void SamplerRootSpanParallel()
    {
        Parallel.For(0, _parallelCalls, new ParallelOptions { MaxDegreeOfParallelism = 4 }, _ =>
        {
            var parameters = new SamplingParameters(default, ActivityTraceId.CreateRandom(), "GET /books", ActivityKind.Server);
            _sampler.ShouldSample(parameters);
        });
    }

    /// <summary>Extracts the query label from a multi-line SQL command.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? SqlQueryLabel()
    {
        return ArkSqlQueryLabel.Extract(_labelledQuery);
    }

    /// <summary>Reads one object through the discriminator-based polymorphic converter.</summary>
    /// <returns>The deserialized shape.</returns>
    [Benchmark]
    public Shape? PolymorphicJsonRead()
    {
        return JsonSerializer.Deserialize<Shape>(_polymorphicJson, _jsonOptions);
    }

    /// <summary>Applies a response ETag to a conditional GET without If-None-Match.</summary>
    /// <returns>The short-circuit result, if any.</returns>
    [Benchmark]
    public IResult? ResponseETag()
    {
        return ArkETag.ApplyResponseETag(_httpContext, "v42-abcdef", conditionalGet: true);
    }

    /// <summary>Shreds 1,000 rows of two derived types into a DataTable.</summary>
    /// <returns>The table.</returns>
    [Benchmark]
    public DataTable PolymorphicDataTable()
    {
        return _rows.ToDataTablePolymorphic();
    }

    /// <summary>Discriminator for <see cref="Shape"/>.</summary>
    public enum ShapeKind
    {
        /// <summary>A circle.</summary>
        Circle,

        /// <summary>A square.</summary>
        Square,
    }

    /// <summary>Polymorphic base type.</summary>
    public abstract class Shape
    {
        /// <summary>Gets or sets the name.</summary>
        public string? Name { get; set; }
    }

    /// <summary>A circle.</summary>
    public sealed class Circle : Shape
    {
        /// <summary>Gets or sets the radius.</summary>
        public double Radius { get; set; }
    }

    /// <summary>A square.</summary>
    public sealed class Square : Shape
    {
        /// <summary>Gets or sets the side.</summary>
        public double Side { get; set; }
    }

    private sealed class ShapeConverter : JsonPolymorphicConverter<Shape, ShapeKind>
    {
        public ShapeConverter()
            : base("Kind")
        {
        }

        protected override Type GetType(ShapeKind discriminatorValue)
        {
            return discriminatorValue == ShapeKind.Circle ? typeof(Circle) : typeof(Square);
        }
    }

    /// <summary>Configures a short in-process .NET 10 benchmark run.</summary>
    public sealed class BenchmarkConfig : ManualConfig
    {
        /// <summary>Initializes the benchmark configuration.</summary>
        public BenchmarkConfig()
        {
            AddJob(Job.InProcess
                .WithLaunchCount(1)
                .WithWarmupCount(5)
                .WithIterationCount(15));
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
