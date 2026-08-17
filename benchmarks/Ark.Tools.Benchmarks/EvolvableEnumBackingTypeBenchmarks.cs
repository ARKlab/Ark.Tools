// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using BenchmarkDotNet.Attributes;

namespace Ark.Tools.Benchmarks;

/// <summary>Compares the default int-backed wrapper with its explicit int-backed form.</summary>
[Config(typeof(EvolvableEnumBenchmarks.EvolvableEnumBenchmarkConfig))]
public class EvolvableEnumBackingTypeBenchmarks
{
    private const string _definedName = "Active";
    private const string _unknownName = "Future";

    private static readonly EvolvableEnum<Status> _defaultDefined = EvolvableEnum<Status>.FromValue(Status.Active);
    private static readonly EvolvableEnum<Status, int> _explicitDefined =
        EvolvableEnum<Status, int>.FromValue(Status.Active);
    private static readonly EvolvableEnum<Status> _defaultUndefined = EvolvableEnum<Status>.FromNumber(999);
    private static readonly EvolvableEnum<Status, int> _explicitUndefined =
        EvolvableEnum<Status, int>.FromNumber(999);

    /// <summary>Measures parsing through the default int-backed wrapper.</summary>
    [Benchmark(Baseline = true)]
    public bool DefaultTryParseDefined()
    {
        return EvolvableEnum<Status>.TryParse(_definedName, out _);
    }

    /// <summary>Measures parsing through the explicit int-backed wrapper.</summary>
    [Benchmark]
    public bool ExplicitIntTryParseDefined()
    {
        return EvolvableEnum<Status, int>.TryParse(_definedName, out _);
    }

    /// <summary>Measures unknown-name parsing through the default int-backed wrapper.</summary>
    [Benchmark]
    public bool DefaultTryParseUnknown()
    {
        return EvolvableEnum<Status>.TryParse(_unknownName, out _);
    }

    /// <summary>Measures unknown-name parsing through the explicit int-backed wrapper.</summary>
    [Benchmark]
    public bool ExplicitIntTryParseUnknown()
    {
        return EvolvableEnum<Status, int>.TryParse(_unknownName, out _);
    }

    /// <summary>Measures defined-value formatting through the default int-backed wrapper.</summary>
    [Benchmark]
    public string DefaultToStringDefined()
    {
        return _defaultDefined.ToString();
    }

    /// <summary>Measures defined-value formatting through the explicit int-backed wrapper.</summary>
    [Benchmark]
    public string ExplicitIntToStringDefined()
    {
        return _explicitDefined.ToString();
    }

    /// <summary>Measures unknown-value formatting through the default int-backed wrapper.</summary>
    [Benchmark]
    public string DefaultToStringUnknown()
    {
        return _defaultUndefined.ToString();
    }

    /// <summary>Measures unknown-value formatting through the explicit int-backed wrapper.</summary>
    [Benchmark]
    public string ExplicitIntToStringUnknown()
    {
        return _explicitUndefined.ToString();
    }

    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
    }
}
