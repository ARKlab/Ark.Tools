// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;
using System.Diagnostics;

using Ark.Tools.Core;
using Ark.Tools.Core.Benchmarks;

// Dependency-free comparison of historical, optimized fallback, and intercepted
// ToDataTableArk implementations, plus MoreLINQ-style row insertion.
//
// Intentionally does NOT depend on BenchmarkDotNet or any other 3rd party
// package: it measures wall-clock elapsed time via Stopwatch and managed
// allocations via GC.GetAllocatedBytesForCurrentThread(), which are both part
// of the BCL. HistoricalBaselineConverter reproduces the pre-optimization
// reflection implementation. InterceptedConvert has a compile-time-known element
// type and is replaced by the generator. GenericFallbackConvert has an open type
// parameter and calls the optimized runtime fallback. RowsAddConverter isolates
// the MoreLINQ-style NewRow/ItemArray/Rows.Add insertion path.
//
// Usage: dotnet run -c Release --project benchmarks/Ark.Tools.Core.Benchmarks

const int WarmupIterations = 50;
const int MeasuredIterations = 20;
int[] sizes = [1, 100, 10_000];

Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}", "ToDataTableArk interceptor comparison"));
Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Warmup iterations: {0}, Measured iterations: {1}", WarmupIterations, MeasuredIterations));
Console.WriteLine();

var baselineRows = new List<BenchmarkRow>();
var fallbackRows = new List<BenchmarkRow>();
var interceptedRows = new List<BenchmarkRow>();
var rowsAddRows = new List<BenchmarkRow>();
foreach (var size in sizes)
{
    var data = BenchmarkEntity.CreateMany(size);
    baselineRows.Add(Measure(data, WarmupIterations, MeasuredIterations, HistoricalBaselineConverter<BenchmarkEntity>.Convert));
    fallbackRows.Add(Measure(data, WarmupIterations, MeasuredIterations, GenericFallbackConvert));
    interceptedRows.Add(Measure(data, WarmupIterations, MeasuredIterations, InterceptedConvert));
    rowsAddRows.Add(Measure(data, WarmupIterations, MeasuredIterations, RowsAddConverter.Convert));
}

PrintMarkdownTable(baselineRows, fallbackRows, interceptedRows, rowsAddRows);

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
    IReadOnlyList<BenchmarkRow> baselineRows,
    IReadOnlyList<BenchmarkRow> fallbackRows,
    IReadOnlyList<BenchmarkRow> interceptedRows,
    IReadOnlyList<BenchmarkRow> rowsAddRows)
{
    Console.WriteLine("| Objects | Historical baseline (ms) | Optimized fallback (ms) | Interceptor LoadDataRow (ms) | Direct Rows.Add (ms) | Interceptor vs baseline | Rows.Add vs LoadDataRow |");
    Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|");
    for (var i = 0; i < fallbackRows.Count; i++)
    {
        var baseline = baselineRows[i];
        var fallback = fallbackRows[i];
        var intercepted = interceptedRows[i];
        var rowsAdd = rowsAddRows[i];
        var interceptorReduction = 1 - intercepted.MedianElapsedMs / baseline.MedianElapsedMs;
        var rowsAddReduction = 1 - rowsAdd.MedianElapsedMs / intercepted.MedianElapsedMs;

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "| {0} | {1:F4} | {2:F4} | {3:F4} | {4:F4} | {5:P1} | {6:P1} |",
            fallback.Count,
            baseline.MedianElapsedMs,
            fallback.MedianElapsedMs,
            intercepted.MedianElapsedMs,
            rowsAdd.MedianElapsedMs,
            interceptorReduction,
            rowsAddReduction));
    }

    Console.WriteLine();
    Console.WriteLine("| Objects | Historical allocated (bytes) | Fallback allocated (bytes) | Interceptor allocated (bytes) | Rows.Add allocated (bytes) |");
    Console.WriteLine("|---:|---:|---:|---:|---:|");
    for (var i = 0; i < fallbackRows.Count; i++)
    {
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "| {0} | {1:N0} | {2:N0} | {3:N0} | {4:N0} |",
            baselineRows[i].Count,
            baselineRows[i].MedianAllocatedBytes,
            fallbackRows[i].MedianAllocatedBytes,
            interceptedRows[i].MedianAllocatedBytes,
            rowsAddRows[i].MedianAllocatedBytes));
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
