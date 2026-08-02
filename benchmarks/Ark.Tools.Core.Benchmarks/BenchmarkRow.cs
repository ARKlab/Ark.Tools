// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Core.Benchmarks;

/// <summary>A single measured benchmark row for a given input size.</summary>
/// <param name="Count">The number of objects converted.</param>
/// <param name="MedianElapsedMs">The median elapsed time, in milliseconds, across all measured iterations.</param>
/// <param name="MeanElapsedMs">The mean elapsed time, in milliseconds, across all measured iterations.</param>
/// <param name="MedianAllocatedBytes">The median managed bytes allocated across all measured iterations.</param>
/// <param name="MeanAllocatedBytes">The mean managed bytes allocated across all measured iterations.</param>
public sealed record BenchmarkRow(int Count, double MedianElapsedMs, double MeanElapsedMs, long MedianAllocatedBytes, long MeanAllocatedBytes);
