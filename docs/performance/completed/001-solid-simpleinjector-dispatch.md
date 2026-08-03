# Replace Solid runtime dispatch

## Completed

The three SimpleInjector processors now cache immutable, compiled handler invokers:

- query invokers are cached per closed `TResult` and runtime query type;
- request invokers are cached per closed `TResponse` and runtime request type;
- command invokers are cached per runtime command type.

The cache never stores a handler or decorator instance. Each dispatch resolves the
closed handler service from SimpleInjector, preserving configured scopes,
lifetimes, decorator ordering, cancellation, and exception behavior.

Behavioral tests cover decorated request/query/command handlers, cancellation,
and exceptions in
`tests/Ark.Tools.Solid.SimpleInjector.Tests/SimpleInjectorProcessorTests.cs`.

## Benchmark

`benchmarks/Ark.Tools.Benchmarks/` contains BenchmarkDotNet comparisons of the
former reflection/dynamic path and the cached processor path, each using a
verified container with a decorator. The 2026-08-03 .NET 10 result was:

| Dispatch | Reflection/dynamic mean | Cached mean | Reflection/dynamic allocated | Cached allocated |
|---|---:|---:|---:|---:|
| Query | 383.19 ns | 85.04 ns | 280 B | 48 B |
| Request | 401.81 ns | 84.74 ns | 280 B | 48 B |
| Command | 437.20 ns | 75.11 ns | 160 B | 48 B |

This is a 77.8%, 78.9%, and 82.8% mean-time reduction respectively, with lower
allocation for every dispatch kind. Raw BenchmarkDotNet output is retained only
as the generated review artifact at
`BenchmarkDotNet.Artifacts/results/Ark.Tools.Benchmarks.ProcessorDispatchBenchmarks-report-github.md`.

## Decision: skip source generation and interceptors

No source generator or interceptor was added. Calls are made through
`IRequestProcessor`, `IQueryProcessor`, and `ICommandProcessor`, whose
parameters expose only the response type and interface contract; the concrete
runtime message type remains unknown. A generated replacement cannot safely
resolve the correct closed SimpleInjector handler while preserving arbitrary
processor implementations, decorators, and scopes without changing call sites
or public APIs. The generic runtime invoker cache removes reflection and dynamic
binder work from warm dispatches without either change.

## Verification

1. `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --configuration Debug`
2. `dotnet run --project benchmarks/Ark.Tools.Benchmarks/Ark.Tools.Benchmarks.csproj --configuration Release --framework net10.0 --no-build`
