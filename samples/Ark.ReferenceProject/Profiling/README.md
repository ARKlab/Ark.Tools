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
  --trace artifacts/reference-profile-default-net10.nettrace
dotnet-trace report artifacts/reference-profile-default-net10.nettrace topN -n 20
```

The profiling host launches `dotnet-trace` after database deployment, host
startup, and warmup. It stops the capture before application shutdown, so setup
and teardown are excluded from the CPU profile. Launch the compiled profiling
DLL directly; do not wrap it in `dotnet run`. The measured `RunIterations` call
waits for the Rebus queue and in-process message count to reach zero before the
trace is stopped.

The profiling host deploys `Ark.Reference.Core.Database.dacpac` before starting
the application, matching the C# deployment used by the integration tests.

The trace is intentionally generated under `artifacts/` and is not committed.

## Demystifier configuration

`DemystifiedExceptionLayoutRenderer` remains available for explicit NLog
registration, but the default `NLogConfigurer` no longer registers it. The
default `${exception:format=ToString,Data}` layout therefore uses NLog's
built-in exception renderer and avoids the demystification cost. The profiling
host measures this default configuration; it does not switch the renderer.

## Post-change trace summary

The Release trace (`--warmup 50 --iterations 300`) was captured after removing
the demystified renderer from the default NLog registration:

| Measurement | Result |
| --- | ---: |
| Measured workload, including Rebus drain | 6 m 59.82 s |
| Business-rule request average | 6.34 ms |
| Business-rule request maximum | 19.95 ms |
| Speedscope evented thread profiles | 27 |
| Summed sampled thread time | 7,438.8 s |

The sampled-thread-time trace sums time across threads. Its inclusive durations
include blocked time and must not be interpreted as CPU time or elapsed wall
clock time.

The top exclusive samples were:

| Function | Exclusive samples |
| --- | ---: |
| `LowLevelLifoSemaphore.WaitForSignal` | 29.36% |
| `Thread.Sleep` | 17.96% |
| `WaitHandle.WaitOneNoCheck` | 12.40% |
| `ManualResetEventSlim.Wait` | 9.53% |
| `Missing Symbol` | 6.02% |
| `WaitAnyMultiple` | 6.00% |
| `Interop+Sys.Read` | 6.00% |

The trace is dominated by synchronization, waits, the Application Insights
aggregation timer, and SQL/network I/O. These are not actionable CPU hotspots
for this workload.

## Flame graph analysis

```bash
dotnet-trace convert artifacts/reference-profile-default-net10.nettrace \
  --format Speedscope --output artifacts/reference-profile-default-net10
speedscope artifacts/reference-profile-default-net10.speedscope.json
```

Selected inclusive frames from the Speedscope flame graph were:

| Frame | Inclusive duration | Interpretation |
| --- | ---: | --- |
| `ThreadPoolWorker.TryReceiveNextMessage()` | 420.52 s | Rebus receive loop while draining |
| `Rebus.DefaultBackoffStrategy.Wait` | 13.88 s | Idle worker backoff |
| `AbstractSqlAsyncContext.CommitAsync()` | 6.39 s | Transaction commit path |
| `ProblemDetailsMiddleware` | 1.60 s | Expected exception response path |
| `BookPrintProcess_CreateRequestHandler` | 1.32 s | Expected business-rule exception |
| `SqlServerExtensions.ReadPagedAsync()` | 301 ms | SQL paging path |
| `Dapper.SqlMapper.QueryMultipleAsync()` | 295 ms | SQL result materialization |
| NLog `ExceptionLayoutRenderer.AppendToString()` | 164 ms | Built-in exception formatting |
| `StackFrameHelper.InitializeSourceInfo()` | 62 ms | Standard stack-frame lookup |
| `DataTableExtensions.ToDataTableArk()` | 15 ms | 300 single-row conversions |

The hottest stacks are:

1. `ThreadPoolWorker.TryReceiveNextMessage` spans the Rebus drain and idle
   period. The `DefaultBackoffStrategy.Wait` portion is idle synchronization,
   not message-processing CPU.
2. `BookPrintProcess_CreateRequestHandler` → ProblemDetails middleware →
   `ArkDefaultExceptionFilterAttribute` → NLog's built-in exception renderer is
   the expected error path. The demystified renderer is absent from the flame
   graph after removing its default registration.
3. SQL transaction commits and TDS/SSL reads dominate the database path and are
   I/O-bound; the trace does not show a materialization CPU hotspot.
4. `ToDataTableArk()` remains negligible at 15 ms for 300 single-row calls.

Do not optimize the thread-pool semaphore, Rebus backoff, SQL socket waits, or
Application Insights timer without a CPU-only trace showing application work
behind them.
