// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;
using System.Text.Json;

using Ark.Tools.Core;
using Ark.Tools.Dapper;
using Ark.Tools.MessagePack;
using Ark.Tools.SystemTextJson;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using MessagePack;

namespace Ark.Tools.Benchmarks;

/// <summary>Per-value conversion and serializer costs of <see cref="EvolvableEnum{TEnum}"/>.</summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class EvolvableEnumConversionBenchmarks
{
    private static readonly EvolvableEnum<Status> _defined = Status.Active;
    private static readonly JsonSerializerOptions _numberJsonOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new EvolvableEnumIntegerJsonConverterFactory() },
    };
    private static readonly JsonSerializerOptions _nameJsonOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new EvolvableEnumJsonConverterFactory() },
    };
    private static readonly MessagePackSerializerOptions _messagePackOptions =
        MessagePackSerializerOptions.Standard.WithEvolvableEnumSupport();
    private static readonly EvolvableEnum<Status>[] _values = Enumerable.Range(0, 100)
        .Select(static index => EvolvableEnum<Status>.FromNumber(index % 3))
        .ToArray();
    private static readonly string _numberJson = JsonSerializer.Serialize(_values, _numberJsonOptions);
    private static readonly byte[] _messagePack = MessagePackSerializer.Serialize(_values, _messagePackOptions);
    private readonly EvolvableEnumTypeHandler<Status> _dapperHandler = new();
    private readonly Parameter _parameter = new();

    /// <summary>Wraps a strict enum value (implicit conversion).</summary>
    /// <returns>The wrapped value.</returns>
    [Benchmark]
    public EvolvableEnum<Status> FromValue()
    {
        return Status.Active;
    }

    /// <summary>Reads the strict enum value back.</summary>
    /// <returns>The strict value.</returns>
    [Benchmark]
    public Status? Value()
    {
        return _defined.Value;
    }

    /// <summary>Converts back with the explicit operator.</summary>
    /// <returns>The strict value.</returns>
    [Benchmark]
    public Status ExplicitCast()
    {
        return (Status)_defined;
    }

    /// <summary>Serializes 100 values as names.</summary>
    /// <returns>The JSON.</returns>
    [Benchmark]
    public string JsonSerializeNames()
    {
        return JsonSerializer.Serialize(_values, _nameJsonOptions);
    }

    /// <summary>Deserializes 100 values written as numbers.</summary>
    /// <returns>The value count.</returns>
    [Benchmark]
    public int JsonDeserializeNumbers()
    {
        return JsonSerializer.Deserialize<EvolvableEnum<Status>[]>(_numberJson, _numberJsonOptions)!.Length;
    }

    /// <summary>Deserializes 100 values from MessagePack.</summary>
    /// <returns>The value count.</returns>
    [Benchmark]
    public int MessagePackDeserialize()
    {
        return MessagePackSerializer.Deserialize<EvolvableEnum<Status>[]>(_messagePack, _messagePackOptions).Length;
    }

    /// <summary>Binds one value as a Dapper string parameter.</summary>
    /// <returns>The bound value.</returns>
    [Benchmark]
    public object? DapperSetName()
    {
        _dapperHandler.SetValue(_parameter, _defined);
        return _parameter.Value;
    }

    /// <summary>Benchmark enum.</summary>
    public enum Status
    {
        /// <summary>Not set.</summary>
        NOT_SET = 0,

        /// <summary>Active.</summary>
        Active = 1,

        /// <summary>Archived.</summary>
        Archived = 2,
    }

    private sealed class Parameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        [AllowNull]
        public string ParameterName { get; set; } = string.Empty;
        [AllowNull]
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
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
