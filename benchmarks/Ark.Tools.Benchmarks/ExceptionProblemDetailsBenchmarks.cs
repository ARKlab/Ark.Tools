// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Core.BusinessRuleViolation;

using BenchmarkDotNet.Attributes;

namespace Ark.Tools.Benchmarks;

[MemoryDiagnoser]
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
        return payload;
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
}
