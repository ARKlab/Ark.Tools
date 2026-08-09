# Ark.Reference profiling

This project uses BenchmarkDotNet with `EventPipeProfiler` CPU sampling to profile
the Ark.Reference API through its integration-test `TestServer`.

Each benchmark invokes one endpoint ten times:

- `PostBook`: `POST /v1/book`
- `GetBook`: `GET /v1/book/{id}`
- `PostPingMessage`: `POST /v1/ping/message`
- `PostBookPrintProcess`: `POST /v1/bookPrintProcess`

The profiler supports three separate SqlClient configurations selected with
`ARK_SQLCLIENT_SWITCH`:

- unset or `baseline`: default SqlClient behavior
- `make-read-async-blocking`: synchronously reads the DONE token
- `experimental-async`: enables the paired continuation switches for async reads

The switch is applied in `GlobalSetup` before the database is deployed or any
`SqlConnection` is created. Do not compare these configurations as BenchmarkDotNet
methods in one process: SqlClient caches the values after first connection access.

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

Run each configuration in a separate process and use a separate artifacts
directory:

```bash
ARK_SQLCLIENT_SWITCH=baseline \
  dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --filter '*PostBook*' --artifacts artifacts/sqlclient-baseline

ARK_SQLCLIENT_SWITCH=make-read-async-blocking \
  dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --filter '*PostBook*' --artifacts artifacts/sqlclient-blocking

ARK_SQLCLIENT_SWITCH=experimental-async \
  dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --filter '*PostBook*' --artifacts artifacts/sqlclient-experimental
```

Repeat the three runs for each endpoint being compared. Compare CPU samples
from the workload-only reports, not BenchmarkDotNet latency statistics.

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

Install the command-line viewers once:

```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global KlutzyNinja.Filtrace
npm install --global speedscope
```

From the Ark.ReferenceProject directory, dump a `dotnet-trace` top-method report
and convert every BenchmarkDotNet trace to SpeedScope:

```bash
find artifacts/BenchmarkDotNet.Artifacts -name '*.nettrace' -print0 |
  while IFS= read -r -d '' trace; do
    dotnet-trace report "$trace" topN -n 30 > "${trace%.nettrace}.topN.txt"
    dotnet-trace convert "$trace" \
      --format Speedscope \
      --output "${trace%.nettrace}"
  done
```

`dotnet-trace report` ranks the complete capture and cannot exclude
`GlobalSetup`, harness, or cleanup frames. Use it to orient the investigation,
not to assign benchmark-only percentages.

For a workload-only report and SpeedScope file, use
[filtrace](https://github.com/JeremyKuhne/filtrace). Its `--benchmark` preset
keeps the BenchmarkDotNet `WorkloadAction` subtree and excludes `GlobalSetup`,
warmup/harness overhead, `IterationCleanup`, and `GlobalCleanup`:

```bash
find artifacts/BenchmarkDotNet.Artifacts -name '*.nettrace' -print0 |
  while IFS= read -r -d '' trace; do
    workload="${trace%.nettrace}.workload"
    filtrace cpu "$trace" --benchmark --top 30 > "$workload.cpu.txt"
    filtrace cpu "$trace" --benchmark --measure inclusive --top 30 \
      > "$workload.inclusive.txt"
    filtrace export "$trace" --benchmark --format speedscope \
      -o "$workload.speedscope.json"
  done
```

Open one filtered profile with:

```bash
speedscope artifacts/BenchmarkDotNet.Artifacts/results/trace.workload.speedscope.json
```

For an unfiltered BenchmarkDotNet `.speedscope.json`, use the **Time Order**
view, search for `WorkloadAction`, and double-click that frame to restrict the
visible range. The filtered export is preferable because its totals use only
the workload subtree.

For every endpoint:

1. Sort the call tree by **Exclusive** duration to find methods doing work
   directly.
2. Sort by **Inclusive** duration and expand application frames to find expensive
   call paths.
3. Inspect the flame graph's widest application-owned stacks. Use the Sandwich
   view to compare total and self time.
4. Verify that the analyzed root is `WorkloadAction`; do not compare a
   whole-capture total with a workload-only duration.
5. Confirm a candidate in its endpoint-specific trace before changing code.

For deeper command-line analysis:

```bash
trace=artifacts/BenchmarkDotNet.Artifacts/results/ProfilingBenchmarks.GetPagedContractsAsync-20260101-120000.nettrace
filtrace info "$trace"
filtrace tree "$trace" --benchmark --max-depth 8
filtrace callers "$trace" ReadPagedAsync --benchmark --callees
filtrace callers "$trace" ProblemDetailsMiddleware --benchmark --callees
```

Sampled thread time includes blocking. Thread-pool waits, Rebus backoff, SQL
socket reads, and timer waits are not CPU optimization candidates unless a
CPU-bound application stack appears beneath them.

### Other open-source analyzers

- [filtrace](https://github.com/JeremyKuhne/filtrace) reads `.nettrace` and
  `.speedscope.json`, reports self/inclusive CPU, callers, call trees, source
  lines, timelines, and diffs, and has the BenchmarkDotNet workload filter used
  above.
- [pvanalyze](https://github.com/adityamandaleeka/pvanalyze) reads `.nettrace`
  cross-platform and provides CPU stacks, caller/callee trees, GC, JIT,
  allocation, exception, event, JSON, and time-window reports. It has no
  BenchmarkDotNet preset, so determine a workload time window first and pass
  `--from` and `--to`.
- [PerfView](https://github.com/microsoft/perfview) provides the most complete
  Windows call-tree and event investigation. Use its include/exclude and
  start/stop filters when the CLI summaries are insufficient.
- [dotnet-trace](https://github.com/dotnet/diagnostics) is the first-party
  collector, converter, and basic top-method reporter.
- [SpeedScope](https://github.com/jlfwong/speedscope) is the interactive
  time-order, left-heavy, and caller/callee viewer; it is not a general
  `.nettrace` event analyzer.

## Optimization candidates

The previous combined Release trace measured 419.82 seconds of wall-clock
workload and 7,438.8 seconds of sampled thread time across 27 thread profiles.
The following values are inclusive sampled-thread durations. Their percentage
denominator is the 7,438.8-second sampled-thread total, not wall-clock time.
Nested frames overlap, so rows must not be added together.

| Candidate frame | Inclusive duration | Share of sampled-thread total | Endpoint and interpretation |
| --- | ---: | ---: | --- |
| `AbstractSqlAsyncContext.CommitAsync` | 6.39 s | 0.0859% | Write endpoints; database commit is primarily external I/O |
| `ProblemDetailsMiddleware` | 1.60 s | 0.0215% | Expected error path; contains downstream handling |
| `BookPrintProcess_CreateRequestHandler` | 1.32 s | 0.0177% | Expected business-rule exception path |
| `SqlServerExtensions.ReadPagedAsync` | 301 ms | 0.0040% | Book reads; includes SQL wait and materialization |
| `Dapper.SqlMapper.QueryMultipleAsync` | 295 ms | 0.0040% | Nested book-read SQL path; do not add to `ReadPagedAsync` |
| NLog `ExceptionLayoutRenderer.AppendToString` | 164 ms | 0.0022% | Exception formatting; compare exclusive CPU before changing logging |
| `StackFrameHelper.InitializeSourceInfo` | 62 ms | 0.0008% | Standard stack-frame source lookup |
| `DataTableExtensions.ToDataTableArk` | 15 ms | 0.0002% | 300 single-row conversions in the former combined workload |

Analyze each candidate in its endpoint-specific workload trace:

1. Compare self and inclusive rankings. A large inclusive duration with little
   self time points to callees or waits rather than the named method.
2. Use `filtrace callers` and `filtrace tree` to preserve call-path context.
3. For SQL paths, inspect database-provider events or PerfView thread-time data
   before attributing socket/TDS time to materialization CPU.
4. For exception formatting, capture an expected-error benchmark and compare
   NLog renderer self time with the whole `WorkloadAction` total.
5. Compare changes with `filtrace diff before.nettrace after.nettrace
   --benchmark`; use traces captured with the same benchmark and settings.

The previous trace did not justify optimizing Rebus idle backoff, thread-pool
semaphores, SQL/network waits, Application Insights timers, or
`ToDataTableArk`.

## SqlClient switch comparison

Use the same endpoint, build, database environment, filter, and profiler
settings for all three invocations. For each `.nettrace`, generate the
workload-only reports above and compare:

- total workload sampled-thread time
- self CPU samples for `AbstractSqlAsyncContext.CommitAsync`
- its inclusive callers/callees, especially SQL socket and wait frames

The `make-read-async-blocking` switch trades thread-pool scalability for
synchronous DONE-token reads. The `experimental-async` configuration requires
both continuation switches and targets broader async read overhead. A switch
is an improvement only when the relevant `CommitAsync` CPU samples decrease
without moving equivalent CPU work into another application-owned frame.

## Demystifier configuration

`DemystifiedExceptionLayoutRenderer` remains available for explicit NLog
registration, but the default `NLogConfigurer` does not register it. These
benchmarks profile NLog's built-in exception renderer.
