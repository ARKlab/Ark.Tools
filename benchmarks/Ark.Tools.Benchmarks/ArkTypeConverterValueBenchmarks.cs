// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

using Ark.Tools.MediatorFramework.MinimalApi;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace Ark.Tools.Benchmarks;

/// <summary>Compares repeated type-converter lookup with cached conversion.</summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ArkTypeConverterValueBenchmarks
{
    private const string _input = "42";

    /// <summary>Measures conversion with a type-converter lookup on every call.</summary>
    /// <returns>The converted wrapper.</returns>
    [Benchmark(Baseline = true)]
    public object? TypeDescriptorLookup()
    {
        var converter = TypeDescriptor.GetConverter(typeof(BenchmarkValue));
        if (!converter.CanConvertFrom(typeof(string)))
            return null;

        var converted = converter.ConvertFrom(null, CultureInfo.InvariantCulture, _input);
        return converted is BenchmarkValue typedValue
            ? new ArkTypeConverterValue<BenchmarkValue>(typedValue)
            : null;
    }

    /// <summary>Measures conversion through the cached generic converter path.</summary>
    /// <returns>The converted wrapper.</returns>
    [Benchmark]
    public object? CachedConverter()
    {
        return ArkTypeConverterValue<BenchmarkValue>.TryParse(
            _input,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;
    }

    [TypeConverter(typeof(BenchmarkValueConverter))]
    private sealed record BenchmarkValue(int Value);

    private sealed class BenchmarkValueConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object value)
        {
            return new BenchmarkValue(int.Parse((string)value, CultureInfo.InvariantCulture));
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
                .WithWarmupCount(1)
                .WithIterationCount(3));
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
