# Ark.Reference profiling

The profiling host runs the application through `TestServer` using the IntegrationTests
configuration. Each measured iteration exercises:

- `POST /v1/book` (SQL write)
- `GET /v1/book/{id}` (SQL read)
- `POST /v1/ping/message` (Rebus and outbox pipeline)
- two `POST /v1/bookPrintProcess` calls (successful request followed by `BusinessRuleViolation`)
- `ToDataTableArk()` on the returned book

The first ten iterations are warmup and are excluded from the elapsed-time summary.

## Run a Release trace

Start the SQL Server and Azurite dependencies and deploy the sample database as
described in the parent project README. From the repository root, run:

```bash
cd samples/Ark.ReferenceProject
dotnet build Ark.Reference.slnx --configuration Release
dotnet tool install --global dotnet-trace
dotnet-trace collect --output artifacts/reference-profile.nettrace \
  -- dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --warmup 10 --iterations 100
dotnet-trace report artifacts/reference-profile.nettrace topN -n 20
```

Launch the compiled profiling DLL directly. Do not wrap it in `dotnet run`;
tracing the `dotnet run` launcher can leave `dotnet-trace` waiting after the
workload has completed.

The profiling host deploys `Ark.Reference.Core.Database.dacpac` before starting
the application, matching the C# deployment used by the integration tests.

The trace is intentionally generated under `artifacts/` and is not committed.

## Trace summary

The representative Release trace (`--warmup 10 --iterations 100`) reported these
top exclusive samples:

| Function | Exclusive samples |
| --- | ---: |
| `LowLevelLifoSemaphore.WaitNative` | 30.50% |
| `WaitHandle.WaitOneNoCheck` | 22.22% |
| `Missing Symbol` | 9.13% |
| `Thread.Sleep` | 7.31% |
| `ManualResetEventSlim.Wait` | 7.11% |
| `LowLevelLifoSemaphore.WaitForSignal` | 6.03% |
| `GC.RunFinalizers` | 5.89% |

The trace is dominated by synchronization, waiting, finalization, and the
intentional progressive-printing delays, not CPU spent in SQL materialization or
`ToDataTableArk()`. Application frames for Rebus, Dapper, and the one-row
`ToDataTableArk()` call are negligible in this workload; larger batches are
needed to measure conversion cost.

## Flame graph and inclusive analysis

Install the open-source MIT-licensed Speedscope viewer and convert the trace:

```bash
npm install --global speedscope@1.25.0
dotnet-trace convert artifacts/reference-profile.nettrace \
  --format Speedscope --output artifacts/reference-profile
speedscope artifacts/reference-profile.speedscope.json
```

The validated Speedscope file contained 24 evented thread profiles spanning
about 24.4 seconds. The following inclusive durations are summed across threads,
so they include blocked time and must not be interpreted as CPU time or elapsed
wall-clock time:

| Repository frame | Inclusive duration | Interpretation |
| --- | ---: | --- |
| `Program.DeployDatabase()` (`Profiling/Program.cs:59-72`) | 17.19 s | Startup-only DACPAC deployment |
| `Program.RunIterations()` (`Profiling/Program.cs:75-112`) | 1.30 s | Measured workload coordinator |
| `RebusProcessorService.StopAsync()` | 806 ms | Shutdown wait for message continuations |
| `BookPrintProcess_CreateRequestHandler.ExecuteAsync()` | 667 ms | Request and expected duplicate-process error path |
| `AbstractSqlAsyncContext.CommitAsync()` | 565 ms | Transaction commit path |
| `ArkStartupBase.Configure()` | 561 ms | HTTP pipeline execution |
| `SqlServerExtensions.ReadPagedAsync()` | 435 ms | SQL paging path |
| `Dapper.SqlMapper.QueryMultipleAsync()` | 427 ms | SQL result materialization |
| `DemystifiedExceptionLayoutRenderer.AppendToString()` | 348 ms | Formatting expected exception responses |
| `DataTableExtensions.ToDataTableArk()` | 11 ms | 100 single-row conversion calls |

The hottest stacks are:

1. `Program.Main` → `DeployDatabase` → `DacServices.Deploy` → SQL client TDS/SSL
   reads. This is setup cost and accounts for most of the SQL inclusive time; it
   should be excluded when comparing steady-state application runs.
2. `BookPrintProcess_CreateRequestHandler.ExecuteAsync` → exception middleware
   → `ArkDefaultExceptionFilterAttribute` → NLog demystified exception
   formatting. The workload intentionally sends one failing request per
   iteration, so this is an expected error-path cost rather than a failure.
3. `CoreDataContextFactory.CreateAsync` → `ReadPagedAsync` →
   `QueryMultipleAsync` → SQL client network reads. The stack is I/O-bound; the
   trace does not show a materialization CPU hotspot.
4. `RebusProcessorService.StopAsync` → `RebusBus.Dispose` →
   `WaitForContinuationsToFinish`. This is shutdown synchronization, not message
   processing CPU.

For a CPU-focused follow-up, capture after database deployment and separate the
expected error path from the successful request path. Do not optimize the
thread-pool semaphore, Rebus backoff, SQL socket waits, or finalizer stacks
without a CPU-only trace showing application work behind them.
