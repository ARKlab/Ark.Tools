// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;
using System.Diagnostics;

using Ark.Tools.Core;
using Ark.Tools.Core.Benchmarks;

// Dependency-free comparison of intercepted and reflection-fallback ToDataTableArk calls.
//
// Intentionally does NOT depend on BenchmarkDotNet or any other 3rd party
// package: it measures wall-clock elapsed time via Stopwatch and managed
// allocations via GC.GetAllocatedBytesForCurrentThread(), which are both part
// of the BCL. InterceptedConvert has a compile-time-known element type and is
// replaced by the generator. GenericFallbackConvert has an open type parameter,
// which is ineligible for interception and therefore calls the runtime fallback.
//
// Usage: dotnet run -c Release --project benchmarks/Ark.Tools.Core.Benchmarks

const int WarmupIterations = 50;
const int MeasuredIterations = 20;
int[] sizes = [1, 100, 10_000];

Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}", "ToDataTableArk interceptor comparison"));
Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Warmup iterations: {0}, Measured iterations: {1}", WarmupIterations, MeasuredIterations));
Console.WriteLine();

var fallbackRows = new List<BenchmarkRow>();
var interceptedRows = new List<BenchmarkRow>();
foreach (var size in sizes)
{
    var data = BenchmarkEntity.CreateMany(size);
    fallbackRows.Add(Measure(data, WarmupIterations, MeasuredIterations, GenericFallbackConvert));
    interceptedRows.Add(Measure(data, WarmupIterations, MeasuredIterations, InterceptedConvert));
}

PrintMarkdownTable(fallbackRows, interceptedRows);

static BenchmarkRow Measure(
    BenchmarkEntity[] data,
    int warmupIterations,
    int measuredIterations,
    Func<BenchmarkEntity[], DataTable> convert)
{
    // Warmup: lets tiered JIT reach steady state and avoids measuring first-call costs.
    for (var i = 0; i < warmupIterations; i++)
    {
        using var warmupTable = convert(data);
    }

    var elapsedTicks = new long[measuredIterations];
    var allocatedBytes = new long[measuredIterations];

    for (var i = 0; i < measuredIterations; i++)
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        using var table = convert(data);

        sw.Stop();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        elapsedTicks[i] = sw.ElapsedTicks;
        allocatedBytes[i] = allocatedAfter - allocatedBefore;
    }

    Array.Sort(elapsedTicks);
    Array.Sort(allocatedBytes);

    var medianElapsedMs = elapsedTicks[measuredIterations / 2] * 1000.0 / Stopwatch.Frequency;
    var meanElapsedMs = elapsedTicks.Average() * 1000.0 / Stopwatch.Frequency;
    var medianAllocated = allocatedBytes[measuredIterations / 2];
    var meanAllocated = (long)allocatedBytes.Average();

    return new BenchmarkRow(data.Length, medianElapsedMs, meanElapsedMs, medianAllocated, meanAllocated);
}

static void PrintMarkdownTable(
    IReadOnlyList<BenchmarkRow> fallbackRows,
    IReadOnlyList<BenchmarkRow> interceptedRows)
{
    Console.WriteLine("| Objects | Fallback median (ms) | Interceptor median (ms) | Time reduction | Fallback allocated (bytes) | Interceptor allocated (bytes) | Allocation reduction |");
    Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|");
    for (var i = 0; i < fallbackRows.Count; i++)
    {
        var fallback = fallbackRows[i];
        var intercepted = interceptedRows[i];
        var timeReduction = 1 - intercepted.MedianElapsedMs / fallback.MedianElapsedMs;
        var allocationReduction = 1 - (double)intercepted.MedianAllocatedBytes / fallback.MedianAllocatedBytes;

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "| {0} | {1:F4} | {2:F4} | {3:P1} | {4:N0} | {5:N0} | {6:P1} |",
            fallback.Count,
            fallback.MedianElapsedMs,
            intercepted.MedianElapsedMs,
            timeReduction,
            fallback.MedianAllocatedBytes,
            intercepted.MedianAllocatedBytes,
            allocationReduction));
    }
}

static DataTable InterceptedConvert(BenchmarkEntity[] source)
{
    return source.ToDataTableArk();
}

static DataTable GenericFallbackConvert<T>(T[] source)
{
    return source.ToDataTableArk();
}
