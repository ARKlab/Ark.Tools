// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Benchmarks;

/// <summary>Compares strict enum and evolvable enum parsing, formatting, and JSON serialization.</summary>
[Config(typeof(EvolvableEnumBenchmarkConfig))]
public class EvolvableEnumBenchmarks
{
    private const string _definedName = "Active";
    private const string _unknownName = "Future";

    private const Status _definedStatus = Status.Active;
    private const Status _undefinedStatus = (Status)999;
    private static readonly EvolvableEnum<Status> _definedEvolvable = EvolvableEnum<Status>.FromValue(Status.Active);
    private static readonly EvolvableEnum<Status> _undefinedEvolvable = EvolvableEnum<Status>.FromNumber(999);
    private static readonly JsonSerializerOptions _strictJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions _evolvableJsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    private static readonly StrictRecord[] _strictRecords = Enumerable.Range(0, 100)
        .Select(index => new StrictRecord((Status)(index % 3), index))
        .ToArray();
    private static readonly EvolvableRecord[] _evolvableRecords = Enumerable.Range(0, 100)
        .Select(index => new EvolvableRecord(EvolvableEnum<Status>.FromNumber(index % 3), index))
        .ToArray();
    private static readonly string _strictJson = JsonSerializer.Serialize(_strictRecords, _strictJsonOptions);
    private static readonly string _evolvableJson = JsonSerializer.Serialize(_evolvableRecords, _evolvableJsonOptions);

    /// <summary>Measures parsing a declared enum name with <c>Enum.TryParse</c>.</summary>
    [Benchmark(Baseline = true)]
    public bool EnumTryParseDefined()
    {
        return Enum.TryParse<Status>(_definedName, ignoreCase: false, out _);
    }

    /// <summary>Measures parsing a declared name with <see cref="EvolvableEnum{TEnum}.TryParse(string, out EvolvableEnum{TEnum})"/>.</summary>
    [Benchmark]
    public bool EvolvableEnumTryParseDefined()
    {
        return EvolvableEnum<Status>.TryParse(_definedName, out _);
    }

    /// <summary>Measures parsing an unknown name with <c>Enum.TryParse</c>.</summary>
    [Benchmark]
    public bool EnumTryParseUnknown()
    {
        return Enum.TryParse<Status>(_unknownName, ignoreCase: false, out _);
    }

    /// <summary>Measures preserving an unknown name with <see cref="EvolvableEnum{TEnum}.TryParse(string, out EvolvableEnum{TEnum})"/>.</summary>
    [Benchmark]
    public bool EvolvableEnumTryParseUnknown()
    {
        return EvolvableEnum<Status>.TryParse(_unknownName, out _);
    }

    /// <summary>Measures formatting a declared enum value with <see cref="Enum.ToString()"/>.</summary>
    [Benchmark]
    public string EnumToStringDefined()
    {
        return _definedStatus.ToString();
    }

    /// <summary>Measures formatting a declared enum value with <see cref="EnumExtensions.AsString{T}(T)"/>.</summary>
    [Benchmark]
    public string EnumAsStringDefined()
    {
        return _definedStatus.AsString();
    }

    /// <summary>Measures formatting a declared value with <see cref="EvolvableEnum{TEnum}.ToString()"/>.</summary>
    [Benchmark]
    public string EvolvableEnumToStringDefined()
    {
        return _definedEvolvable.ToString();
    }

    /// <summary>Measures formatting an undefined enum value with <see cref="Enum.ToString()"/>.</summary>
    [Benchmark]
    public string EnumToStringUndefined()
    {
        return _undefinedStatus.ToString();
    }

    /// <summary>Measures formatting an undefined enum value with <see cref="EnumExtensions.AsString{T}(T)"/>.</summary>
    [Benchmark]
    public string EnumAsStringUndefined()
    {
        return _undefinedStatus.AsString();
    }

    /// <summary>Measures formatting an unknown numeric value with <see cref="EvolvableEnum{TEnum}.ToString()"/>.</summary>
    [Benchmark]
    public string EvolvableEnumToStringUndefined()
    {
        return _undefinedEvolvable.ToString();
    }

    /// <summary>Measures string JSON serialization of records containing a strict enum.</summary>
    [Benchmark]
    public string JsonSerializeEnumArray()
    {
        return JsonSerializer.Serialize(_strictRecords, _strictJsonOptions);
    }

    /// <summary>Measures string JSON serialization of records containing an evolvable enum.</summary>
    [Benchmark]
    public string JsonSerializeEvolvableEnumArray()
    {
        return JsonSerializer.Serialize(_evolvableRecords, _evolvableJsonOptions);
    }

    /// <summary>Measures string JSON deserialization of records containing a strict enum.</summary>
    [Benchmark]
    public int JsonDeserializeEnumArray()
    {
        return JsonSerializer.Deserialize<StrictRecord[]>(_strictJson, _strictJsonOptions)!.Length;
    }

    /// <summary>Measures string JSON deserialization of records containing an evolvable enum.</summary>
    [Benchmark]
    public int JsonDeserializeEvolvableEnumArray()
    {
        return JsonSerializer.Deserialize<EvolvableRecord[]>(_evolvableJson, _evolvableJsonOptions)!.Length;
    }

    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
        Archived = 2,
    }

    private sealed record StrictRecord(Status Status, int Index);

    private sealed record EvolvableRecord(EvolvableEnum<Status> Status, int Index);

    /// <summary>Uses BenchmarkDotNet's adaptive measurement runs and reports managed allocations.</summary>
    public sealed class EvolvableEnumBenchmarkConfig : ManualConfig
    {
        /// <summary>Initializes the benchmark job and memory diagnoser.</summary>
        public EvolvableEnumBenchmarkConfig()
        {
            AddJob(Job.InProcess);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
