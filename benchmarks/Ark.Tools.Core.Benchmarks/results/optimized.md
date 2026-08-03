# ToDataTableArk benchmark - optimized (after fallback optimization)

Captured with: `dotnet run -c Release --project benchmarks/Ark.Tools.Core.Benchmarks -- optimized`

Same environment/fixture/methodology as `baseline.md`, after caching compiled
Expression-tree member-access + conversion delegates (and the derived DataColumn
schema) once per `T` in `ShredObjectToDataTable<T>`'s static constructor.

**Important methodology note:** compiled Expression-tree delegates run through a
dynamically-generated method (`System.Reflection.Emit.DynamicMethod`) that needs
more call volume than plain reflection before the tiered JIT promotes it to fully
optimized code. The initial 5-iteration warmup used while developing this harness
under-warmed the optimized path only, producing misleading numbers where the
optimized path looked *slower* for 10,000 objects. The harness was corrected to use
50 warmup iterations (`WarmupIterations = 50` in `Program.cs`) so both
implementations reach JIT steady state before measurement; the numbers below (and
the re-captured baseline numbers used for the deltas) use that corrected harness.

| Objects | Median time (ms) | Mean time (ms) | Median allocated (bytes) | Mean allocated (bytes) | Allocated/object (bytes) |
|---:|---:|---:|---:|---:|---:|
| 1 | 0.0316 | 0.0368 | 20,072 | 20,072 | 20,072.0 |
| 100 | 0.1654 | 0.1712 | 66,200 | 66,200 | 662.0 |
| 10000 | 6.4313 | 6.5471 | 7,343,168 | 7,343,168 | 734.3 |

Repeat runs (warmup=50), confirming stability:

| Run | 1 obj (ms) | 100 obj (ms) | 10000 obj (ms) |
|---|---:|---:|---:|
| optimized-w50-1 | 0.0319 | 0.1628 | 6.4873 |
| optimized-w50-2 | 0.0314 | 0.1597 | 6.4857 |
| optimized-w50-3 | 0.0305 | 0.1578 | 6.6016 |
| optimized-final | 0.0316 | 0.1654 | 6.4313 |

## Baseline (re-captured with the corrected 50-iteration warmup, for a fair comparison)

| Objects | Median time (ms) | Mean time (ms) | Median allocated (bytes) | Mean allocated (bytes) | Allocated/object (bytes) |
|---:|---:|---:|---:|---:|---:|
| 1 | 0.0374 | 0.0419 | 20,296 | 20,296 | 20,296.0 |
| 100 | 0.2062 | 0.2141 | 68,800 | 68,800 | 688.0 |
| 10000 | 8.5403 | 8.4719 | 7,583,368 | 7,583,368 | 758.3 |

(mean of baseline-w50-1/2/3 runs: 8.8145/8.3654/8.4411 ms median at 10000 objects)

## Deltas (optimized vs. baseline, warmup=50 methodology, median timings)

| Objects | Baseline (ms) | Optimized (ms) | Time delta | Baseline alloc (B) | Optimized alloc (B) | Alloc delta |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.0374 | 0.0316 | **-15.5%** | 20,296 | 20,072 | -1.1% |
| 100 | 0.2062 | 0.1654 | **-19.8%** | 68,800 | 66,200 | -3.8% |
| 10000 | 8.5403 | 6.4313 | **-24.7%** | 7,583,368 | 7,343,168 | -3.2% |

Time improves 15-25% across all sizes (more pronounced as N grows, since the fixed
one-time cost of building the compiled accessor plan is amortized over more rows).
Allocations drop by a few percent: the row-shredding logic itself allocates the same
number of `object[]` row buffers and boxes the same number of values either way
(boxing is unavoidable because `DataTable.LoadDataRow` takes `object?[]`), but the
optimized path no longer allocates on the `value.GetType()` / `Nullable.GetUnderlyingType()`
/ enum boxing-for-comparison path that the original per-value type-switch performed.

## Earlier (misleading) numbers superseded by this corrected methodology

An initial harness run with only 5 warmup iterations measured the optimized 10,000-object
case at ~17-18ms (i.e. *slower* than the ~13-16ms baseline measured with the same
under-warmed harness). This was root-caused via an isolated micro-benchmark
(`Expression.Compile()`-produced delegates vs. `PropertyInfo.GetValue`) that reproduced
the same effect: `DynamicMethod`-backed delegates need materially more invocations before
the tiered JIT promotes them to optimized code than plain reflection invocation does in
.NET 8/10 (whose `MethodInvoker` reflection path is itself already highly optimized). Once
warmup was increased to 50 iterations, both implementations reach steady state and the
compiled-delegate approach is consistently and reproducibly faster, as shown above. This is
recorded here for transparency about the investigation, not because the numbers are used
in the final comparison.
