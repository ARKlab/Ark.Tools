// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Core.BusinessRuleViolation;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using Microsoft.AspNetCore.Mvc;

namespace Ark.Tools.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public class ExceptionProblemDetailsBenchmarks
{
    private readonly BusinessRuleViolationException _empty = new(new EmptyViolation());
    private readonly BusinessRuleViolationException _single = new(new SinglePropertyViolation { Property = "value" });
    private readonly BusinessRuleViolationException _several = new(new SeveralPropertiesViolation
    {
        Count = 7,
        Enabled = true,
        Name = "name",
    });

    [Benchmark]
    public object ReflectionEmpty()
    {
        return MapWithReflection(_empty);
    }

    [Benchmark]
    public object CachedEmpty()
    {
        return ExceptionProblemDetailsMapper.Map(_empty);
    }

    [Benchmark]
    public object ReflectionSingle()
    {
        return MapWithReflection(_single);
    }

    [Benchmark]
    public object CachedSingle()
    {
        return ExceptionProblemDetailsMapper.Map(_single);
    }

    [Benchmark]
    public object ReflectionSeveral()
    {
        return MapWithReflection(_several);
    }

    [Benchmark]
    public object CachedSeveral()
    {
        return ExceptionProblemDetailsMapper.Map(_several);
    }

    private static object MapWithReflection(BusinessRuleViolationException exception)
    {
        var violation = exception.BusinessRuleViolation;
        var payload = violation
            .GetType()
            .GetProperties()
            .Where(property => property.DeclaringType != typeof(BusinessRuleViolation))
            .ToDictionary(
                property => property.Name,
                property => property.GetValue(violation),
                StringComparer.Ordinal);
        payload["type"] = violation.GetType().Name;
        payload["title"] = violation.Title;
        payload["status"] = violation.Status;
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{violation.Status}",
            Status = violation.Status,
            Title = violation.Title,
            Detail = violation.Detail,
        };
        problemDetails.Extensions["businessRuleViolation"] = payload;
        return problemDetails;
    }

    private sealed class EmptyViolation() : BusinessRuleViolation("empty");

    private sealed class SinglePropertyViolation() : BusinessRuleViolation("single")
    {
        public string? Property { get; set; }
    }

    private sealed class SeveralPropertiesViolation() : BusinessRuleViolation("several")
    {
        public int Count { get; set; }
        public bool Enabled { get; set; }
        public string? Name { get; set; }
    }

    public sealed class BenchmarkConfig : ManualConfig
    {
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
