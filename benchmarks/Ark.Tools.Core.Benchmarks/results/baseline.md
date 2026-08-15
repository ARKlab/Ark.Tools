# ToDataTableArk benchmark - baseline (before fallback optimization)

Captured with: `dotnet run -c Release --project benchmarks/Ark.Tools.Core.Benchmarks -- baseline`

Environment: Linux container, .NET 10.0.302 SDK, Server GC disabled, `BenchmarkEntity`
with 10 mixed-type properties (int, string, double, decimal, bool, `int?`, `DateTime`,
enum, `Guid`, NodaTime `LocalDate`). This reflects the reflection-based fallback in
`Ark.Tools.Core.DataTableExtensions` (`ShredObjectToDataTable<T>`) **prior to** caching
compiled member-access/conversion delegates, i.e. every row invokes
`FieldInfo.GetValue`/`PropertyInfo.GetValue` reflection calls plus a runtime
`value.GetType()` + enum/NodaTime type-switch per value on every row.

## Numbers used for the final comparison (50 warmup + 20 measured iterations)

See `optimized.md` for why the harness uses 50 (not 5) warmup iterations - it is
required to reach JIT steady state for the optimized implementation's compiled
Expression-tree delegates, and was applied identically to this baseline capture so
the comparison is apples-to-apples.

| Objects | Median time (ms) | Mean time (ms) | Median allocated (bytes) | Mean allocated (bytes) | Allocated/object (bytes) |
|---:|---:|---:|---:|---:|---:|
| 1 | 0.0374 | 0.0419 | 20,296 | 20,296 | 20,296.0 |
| 100 | 0.2062 | 0.2141 | 68,800 | 68,800 | 688.0 |
| 10000 | 8.5403 | 8.4719 | 7,583,368 | 7,583,368 | 758.3 |

Three repeat runs (allocation counts are deterministic and reproduced exactly across
all runs; timing has normal CI-hardware noise but is stable within ~5%):

| Run | 1 obj (ms) | 100 obj (ms) | 10000 obj (ms) |
|---|---:|---:|---:|
| baseline-w50-1 | 0.0371 | 0.2057 | 8.8145 |
| baseline-w50-2 | 0.0373 | 0.2064 | 8.3654 |
| baseline-w50-3 | 0.0379 | 0.2065 | 8.4411 |

## Original 5-warmup-iteration capture (superseded, kept for the record)

The very first baseline capture used only 5 warmup iterations. Allocation numbers are
identical to the corrected run above (allocation is deterministic and warmup-independent);
timings are noisier but consistent with the corrected numbers within measurement error:

| Objects | Median time (ms) | Mean time (ms) | Median allocated (bytes) |
|---:|---:|---:|---:|
| 1 | 0.0411 | 0.0481 | 20,296 |
| 100 | 0.2126 | 0.2169 | 68,800 |
| 10000 | 13.4744 | 13.7833 | 7,583,368 |

See `optimized.md` for the full explanation, the optimized-side numbers, and the
final delta table.
