// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

using Ark.Tools.Core;
using Ark.Tools.Core.Benchmarks;

// Dependency-free micro-benchmark harness for DataTableExtensions.ToDataTableArk.
//
// Intentionally does NOT depend on BenchmarkDotNet or any other 3rd party
// package: it measures wall-clock elapsed time via Stopwatch and managed
// allocations via GC.GetAllocatedBytesForCurrentThread(), which are both part
// of the BCL. This project purposefully does NOT reference the
// Ark.Tools.Core.Analyzers generator project as an analyzer, so every call to
// ToDataTableArk() below always goes through the reflection-based fallback in
// Ark.Tools.Core.DataTableExtensions (ShredObjectToDataTable<T>), regardless of
// whether the C# 14 interceptor generator is active elsewhere in the solution.
// This lets the same harness be run unmodified before and after the fallback
// optimization to produce directly comparable baseline/optimized numbers.
//
// Usage: dotnet run -c Release --project benchmarks/Ark.Tools.Core.Benchmarks -- [label]
// "label" (e.g. "baseline" or "optimized") is only used to title the output.

var label = args.Length > 0 ? args[0] : "run";
const int WarmupIterations = 50;
const int MeasuredIterations = 20;
int[] sizes = [1, 100, 10_000];

Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "ToDataTableArk benchmark ({0})", label));
Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Warmup iterations: {0}, Measured iterations: {1}", WarmupIterations, MeasuredIterations));
Console.WriteLine();

var rows = new List<BenchmarkRow>();
foreach (var size in sizes)
{
    rows.Add(Measure(size, WarmupIterations, MeasuredIterations));
}

PrintMarkdownTable(label, rows);

static BenchmarkRow Measure(int count, int warmupIterations, int measuredIterations)
{
    var data = BenchmarkEntity.CreateMany(count);

    // Warmup: lets tiered JIT reach steady state and avoids measuring first-call costs.
    for (var i = 0; i < warmupIterations; i++)
    {
        using var warmupTable = data.ToDataTableArk();
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

        using var table = data.ToDataTableArk();

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

    return new BenchmarkRow(count, medianElapsedMs, meanElapsedMs, medianAllocated, meanAllocated);
}

static void PrintMarkdownTable(string label, IReadOnlyList<BenchmarkRow> rows)
{
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "### Results: {0}", label));
    Console.WriteLine();
    Console.WriteLine("| Objects | Median time (ms) | Mean time (ms) | Median allocated (bytes) | Mean allocated (bytes) | Allocated/object (bytes) |");
    Console.WriteLine("|---:|---:|---:|---:|---:|---:|");
    foreach (var row in rows)
    {
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "| {0} | {1:F4} | {2:F4} | {3:N0} | {4:N0} | {5:N1} |",
            row.Count,
            row.MedianElapsedMs,
            row.MeanElapsedMs,
            row.MedianAllocatedBytes,
            row.MeanAllocatedBytes,
            row.Count == 0 ? 0 : (double)row.MedianAllocatedBytes / row.Count));
    }
}
