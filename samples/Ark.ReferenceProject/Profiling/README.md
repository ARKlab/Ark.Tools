# Ark.Reference profiling

The profiling host runs the application through `TestServer` using the IntegrationTests
configuration. Each measured iteration exercises:

- `POST /v1/book` (SQL write)
- `GET /v1/book/{id}` (SQL read)
- `POST /v1/ping/message` (Rebus and outbox pipeline)
- two `POST /v1/bookPrintProcess` calls (successful request followed by `BusinessRuleViolation`)
- `ToDataTableArk()` on the returned book

The default ten warmup iterations are excluded from the elapsed-time summary. Both
values can be changed with `--warmup` and `--iterations`.

## Run a Release trace

Start the SQL Server and Azurite dependencies and deploy the sample database as
described in the parent project README. From the repository root, run:

```bash
cd samples/Ark.ReferenceProject
dotnet build Ark.Reference.slnx --configuration Release
dotnet tool install --global dotnet-trace
dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --warmup 50 --iterations 300 \
  --trace artifacts/reference-profile.nettrace
dotnet-trace report artifacts/reference-profile.nettrace topN -n 20
```

The profiling host launches `dotnet-trace` after database deployment, host
startup, and warmup. It stops the capture before application shutdown, so setup
and teardown are excluded from the CPU profile. Launch the compiled profiling
DLL directly; do not wrap it in `dotnet run`.

The profiling host deploys `Ark.Reference.Core.Database.dacpac` before starting
the application, matching the C# deployment used by the integration tests.

The trace is intentionally generated under `artifacts/` and is not committed.

## Demystifier evaluation on .NET 10

`Ben.Demystifier` 0.4.1 targets .NET Standard 2.0/2.1 and runs on .NET 10
through the standard compatibility contract. It has no .NET 10-specific
implementation. Its value is diagnostic: `ExceptionExtensions.Demystify()`
walks exception frames and formats compiler-generated async, iterator, lambda,
and generic methods into readable stack traces. It does not change exception
behavior or request payloads.

Ark.Tools invokes it only when NLog renders an exception with
`${exception:format=ToString,Data}`. Therefore its cost is paid on exception
logging, not on successful requests. The formatting path performs additional
stack-frame resolution and reflection and allocates the formatted trace.

The profiling host supports an A/B run without removing the package:

```bash
dotnet Profiling/bin/Release/net10.0/Ark.Reference.Profiling.dll \
  --without-demystifier \
  --warmup 50 --iterations 300 \
  --trace artifacts/reference-profile-without-demystifier.nettrace
```

The following paired Release runs used 50 warmup iterations, 300 measured
iterations, one expected business-rule exception per iteration, and
post-warmup `dotnet-sampled-thread-time` traces:

| Run | Demystifier | Total workload | Exception request average | Exception request maximum |
| --- | --- | ---: | ---: | ---: |
| A | enabled | 10.52 s | 6.71 ms | 14.27 ms |
| A | disabled | 18.15 s | 4.48 ms | 105.72 ms |
| B | enabled | 16.88 s | 6.24 ms | 131.72 ms |
| B | disabled | 13.93 s | 4.97 ms | 98.16 ms |

The exception request itself was consistently faster without Demystifier:
33% faster in run A, 20% faster in run B, and 27% faster when comparing the
averages across both runs. The total mixed workload is noisy because it includes SQL, Rebus,
thread-pool scheduling, and intentional delays; its direction changed between
runs, so it is not evidence of a whole-application latency regression.

The CPU profile changed in the expected exception-formatting path:

| Profile frame | Enabled | Disabled |
| --- | ---: | ---: |
| `ExceptionExtensions.Demystify` (inclusive) | 0.13% | absent |
| `DemystifiedExceptionLayoutRenderer.AppendToString` (inclusive) | 0.11% | 0.04% |
| `StackFrameHelper.InitializeSourceInfo` (exclusive) | 0.09% | 0.02% |

The overall profile remains dominated by waits, sleeps, SQL/network I/O, and
Rebus synchronization. Removing Demystifier therefore produces a noticeable
method-level CPU change and a measurable latency reduction on exception-heavy
requests, but not a reliable whole-workload CPU or latency improvement.

**Recommendation:** keep Demystifier enabled for production diagnostics when
readable async stack traces are useful. Disable or remove it only for workloads
with a high volume of synchronously rendered exceptions where the diagnostic
benefit does not justify the formatting cost. Use `--without-demystifier` to
validate that trade-off against an application-specific exception rate.

## Historical full-process trace summary

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

The post-warmup CPU comparison above separates setup and shutdown from the
steady-state workload. Do not optimize the thread-pool semaphore, Rebus
backoff, SQL socket waits, or finalizer stacks without a CPU-only trace showing
application work behind them.
