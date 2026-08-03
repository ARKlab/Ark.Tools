# ToDataTableArk interceptor comparison

Captured with:

```text
dotnet run --no-build --configuration Release \
  --project benchmarks/Ark.Tools.Core.Benchmarks/Ark.Tools.Core.Benchmarks.csproj
```

Environment: Linux container, .NET 10.0.302 SDK, workstation GC, 50 warmup
iterations, and 20 measured iterations per result. Each path converts the same
`BenchmarkEntity` arrays with 10 mixed-type properties:

- **Historical baseline** reproduces the implementation before commit
  `dd26946f`: cached reflection metadata, per-value `GetValue`, runtime type
  inspection/conversion, and `LoadDataRow`.
- **Optimized fallback** calls `ToDataTableArk<T>` from a generic method, where
  `T` is open at compilation and cannot be intercepted. This measures the
  improved compiled-accessor fallback.
- **Interceptor** calls `ToDataTableArk<BenchmarkEntity>` with a compile-time-known
  element type. The generator emits direct member access and uses `LoadDataRow`.
- **Direct Rows.Add** emits the same direct values but fills rows using MoreLINQ's
  [`NewRow`/`ItemArray`/`Rows.Add` approach][morelinq].

All paths call `BeginLoadData` before filling rows and `EndLoadData` from a
`finally` block, restoring events, indexes, and constraints even if enumeration
or conversion throws.

[morelinq]: https://github.com/morelinq/MoreLINQ/blob/master/MoreLinq/ToDataTable.cs#L140

## Final repeat runs

| Run | Objects | Historical baseline (ms) | Optimized fallback (ms) | Interceptor `LoadDataRow` (ms) | Direct `Rows.Add` (ms) | Interceptor vs baseline | `Rows.Add` vs `LoadDataRow` |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0.0257 | 0.0194 | 0.0183 | 0.0163 | 28.8% | 11.0% |
| 1 | 100 | 0.1578 | 0.1006 | 0.0760 | 0.0852 | 51.8% | -12.1% |
| 1 | 10,000 | 5.6344 | 4.0262 | 3.8934 | 4.4886 | 30.9% | -15.3% |
| 2 | 1 | 0.0276 | 0.0226 | 0.0206 | 0.0205 | 25.5% | 0.2% |
| 2 | 100 | 0.1635 | 0.1053 | 0.0783 | 0.0854 | 52.1% | -9.1% |
| 2 | 10,000 | 5.7973 | 4.0703 | 4.0240 | 4.5502 | 30.6% | -13.1% |
| 3 | 1 | 0.0241 | 0.0193 | 0.0161 | 0.0210 | 33.3% | -30.1% |
| 3 | 100 | 0.1644 | 0.0984 | 0.0745 | 0.0911 | 54.7% | -22.3% |
| 3 | 10,000 | 5.6064 | 4.0195 | 3.9102 | 4.4809 | 30.3% | -14.6% |

Allocations were deterministic across all three runs:

| Objects | Historical allocated (bytes) | Fallback allocated (bytes) | Interceptor allocated (bytes) | `Rows.Add` allocated (bytes) |
|---:|---:|---:|---:|---:|
| 1 | 22,872 | 20,072 | 20,072 | 20,040 |
| 100 | 71,208 | 66,200 | 66,200 | 66,168 |
| 10,000 | 7,585,776 | 7,343,168 | 7,343,168 | 7,343,136 |

## Conclusion

Against the actual pre-optimization baseline, the interceptor consistently
reduces median execution time:

- **1 object:** 25.5-33.3%
- **100 objects:** 51.8-54.7%
- **10,000 objects:** 30.3-30.9%

It also reduces allocations by 2,800 bytes for one object, 5,008 bytes for 100
objects, and 242,608 bytes for 10,000 objects.

`Rows.Add` is noisy for one object, where absolute differences are only a few
microseconds. For representative bulk inputs, it is consistently slower than
`LoadDataRow`: 9.1-22.3% slower at 100 objects and 13.1-15.3% slower at 10,000
objects. It also changes row-state semantics: `LoadDataRow(values, true)` accepts
new rows, while `Rows.Add` leaves them in the `Added` state. Keep `LoadDataRow`
for both performance and backward compatibility.
