# ToDataTableArk interceptor comparison

Captured with:

```text
dotnet run --no-build --configuration Release \
  --project benchmarks/Ark.Tools.Core.Benchmarks/Ark.Tools.Core.Benchmarks.csproj
```

Environment: Linux container, .NET 10.0.302 SDK, workstation GC, 50 warmup
iterations, and 20 measured iterations. Both paths convert the same
`BenchmarkEntity` arrays with 10 mixed-type properties:

- **Fallback** calls `ToDataTableArk<T>` from a generic method, where `T` is open
  at compilation and therefore cannot be intercepted.
- **Interceptor** calls `ToDataTableArk<BenchmarkEntity>` from a method with a
  compile-time-known element type, allowing the source generator to replace it
  with direct member access.

## Repeat runs

| Run | Objects | Fallback median (ms) | Interceptor median (ms) | Time reduction | Fallback allocated (bytes) | Interceptor allocated (bytes) |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0.0330 | 0.0271 | 17.7% | 20,072 | 20,072 |
| 1 | 100 | 0.1602 | 0.1195 | 25.4% | 66,200 | 66,200 |
| 1 | 10,000 | 6.6026 | 6.2394 | 5.5% | 7,343,168 | 7,343,168 |
| 2 | 1 | 0.0312 | 0.0272 | 13.0% | 20,072 | 20,072 |
| 2 | 100 | 0.1563 | 0.1196 | 23.5% | 66,200 | 66,200 |
| 2 | 10,000 | 6.5160 | 6.2462 | 4.1% | 7,343,168 | 7,343,168 |
| 3 | 1 | 0.0311 | 0.0258 | 17.1% | 20,072 | 20,072 |
| 3 | 100 | 0.1583 | 0.1227 | 22.5% | 66,200 | 66,200 |
| 3 | 10,000 | 6.5969 | 6.3444 | 3.8% | 7,343,168 | 7,343,168 |

## Conclusion

The interceptor is consistently faster across all three input sizes and repeat
runs. Median time improves by 13.0-17.7% for one object, 22.5-25.4% for 100
objects, and 3.8-5.5% for 10,000 objects. Managed allocations are unchanged
because both paths allocate the same `DataTable`, rows, and boxed values. At
10,000 objects, `DataTable.LoadDataRow` dominates total execution time, reducing
the relative impact of direct member access.
