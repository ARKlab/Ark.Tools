# Ark.Reference profiling

This project uses BenchmarkDotNet with `EventPipeProfiler` CPU sampling to profile
the Ark.Reference API through its integration-test `TestServer`.

Each benchmark invokes one endpoint ten times:

- `PostBook`: `POST /v1/book`
- `GetBook`: `GET /v1/book/{id}`
- `PostPingMessage`: `POST /v1/ping/message`
- `PostBookPrintProcess`: `POST /v1/bookPrintProcess`

`GlobalSetup` drops and recreates the database from
`Ark.Reference.Core.Database.dacpac`, starts the host, and creates seed books.
It does not upgrade an existing schema. `IterationCleanup` waits for the Rebus
in-memory queue and in-process handlers to become idle. The idle wait has a
15-minute timeout.

## Run the benchmarks

Start SQL Server and Azurite as described in the parent README. From the
Ark.ReferenceProject directory, run:

```bash
dotnet build Ark.Reference.slnx --configuration Release
dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --filter '*' \
  --artifacts artifacts/BenchmarkDotNet.Artifacts
```

Run one endpoint by changing the filter:

```bash
dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --filter '*PostPingMessage*' \
  --artifacts artifacts/BenchmarkDotNet.Artifacts
```

BenchmarkDotNet executes the JIT stages, three warmup iterations, and ten
measured iterations. Each iteration invokes one batch of ten requests, for 100
measured requests per benchmark. This batching lets `IterationCleanup` drain
Rebus once per ten requests instead of once per request. `EventPipeProfiler`
performs an additional profiling run and writes one `.nettrace` and one
`.speedscope.json` file per benchmark under the artifacts directory. Benchmark
and trace artifacts are intentionally ignored by Git. BenchmarkDotNet builds
and executes its generated benchmark project in Release configuration.

## Analyze the traces

Open `.nettrace` files in Visual Studio Profiler or PerfView. Open
`.speedscope.json` files in [SpeedScope](https://www.speedscope.app/).

For every endpoint:

1. Sort the call tree by **Exclusive** duration to find methods doing work
   directly.
2. Sort by **Inclusive** duration and expand application frames to find expensive
   call paths.
3. Inspect the flame graph's widest application-owned stacks. Use the Sandwich
   view to compare total and self time.
4. Ignore host setup and seed creation, which run in `GlobalSetup`, and separate
   `IterationCleanup` Rebus draining from request handling.
5. Confirm a candidate in its endpoint-specific trace before changing code.

`dotnet-trace` can also print the top sampled methods:

```bash
dotnet-trace report <trace.nettrace> topN -n 30
```

Sampled thread time includes blocking. Thread-pool waits, Rebus backoff, SQL
socket reads, and timer waits are not CPU optimization candidates unless a
CPU-bound application stack appears beneath them.

## Optimization candidates

The previous combined trace was dominated by synchronization and I/O. Its
application-owned durations identified these candidates for endpoint-specific
verification:

| Candidate | Endpoint | Verification |
| --- | --- | --- |
| Exception formatting in `ProblemDetailsMiddleware` and NLog | Expected error responses | Compare exclusive exception-rendering time before changing logging |
| SQL materialization in `ReadPagedAsync` and `QueryMultipleAsync` | Book reads | Separate materialization CPU from TDS/SSL wait time |
| SQL transaction commit paths | Write endpoints | Optimize only application work around commits; database I/O is external |
| `ToDataTableArk` conversion | Non-endpoint utility work | Not present in these endpoint benchmarks; profile separately if required |

The previous trace did not justify optimizing Rebus idle backoff, thread-pool
semaphores, SQL/network waits, Application Insights timers, or
`ToDataTableArk`.

## Demystifier configuration

`DemystifiedExceptionLayoutRenderer` remains available for explicit NLog
registration, but the default `NLogConfigurer` does not register it. These
benchmarks profile NLog's built-in exception renderer.
