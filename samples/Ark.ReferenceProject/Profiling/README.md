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
DLL directly; do not wrap it in `dotnet run`.

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
| Measured workload | 8.26 s |
| Business-rule request average | 5.97 ms |
| Business-rule request maximum | 9.90 ms |
| Speedscope evented thread profiles | 20 |
| Summed sampled thread time | 155.1 s |
| Aggregated CPU time | 10.3 s (6.64%) |

The sampled-thread-time trace sums time across threads. Its inclusive durations
include blocked time and must not be interpreted as CPU time or elapsed wall
clock time.

The top exclusive samples were:

| Function | Exclusive samples |
| --- | ---: |
| `LowLevelLifoSemaphore.WaitForSignal` | 28.02% |
| `WaitHandle.WaitOneNoCheck` | 19.99% |
| `Thread.Sleep` | 16.16% |
| `ManualResetEventSlim.Wait` | 13.26% |
| `Missing Symbol` | 5.66% |
| `Interop+Sys.Read` | 5.39% |
| `WaitAnyMultiple` | 5.39% |

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
| `Rebus.DefaultBackoffStrategy.Wait` | 8.34 s | Idle worker backoff |
| SQL transaction/network frames | 2.66 s | SQL Server I/O |
| `AbstractSqlAsyncContext.CommitAsync()` | 1.58 s | Transaction commit path |
| `ProblemDetailsMiddleware` | 1.52 s | Expected exception response path |
| `BookPrintProcess_CreateRequestHandler` | 1.43 s | Expected business-rule exception |
| `SqlServerExtensions.ReadPagedAsync()` | 1.11 s | SQL paging path |
| `Dapper.SqlMapper.QueryMultipleAsync()` | 1.10 s | SQL result materialization |
| NLog `ExceptionLayoutRenderer.AppendToString()` | 126 ms | Built-in exception formatting |
| `StackFrameHelper.InitializeSourceInfo()` | 36 ms | Standard stack-frame lookup |
| `DataTableExtensions.ToDataTableArk()` | 9.7 ms | 300 single-row conversions |

The hottest stacks are:

1. `Rebus.DefaultBackoffStrategy.Wait` occupies the worker for the full capture
   while it waits for messages; this is idle synchronization, not message
   processing CPU.
2. `BookPrintProcess_CreateRequestHandler` → ProblemDetails middleware →
   `ArkDefaultExceptionFilterAttribute` → NLog's built-in exception renderer is
   the expected error path. The demystified renderer is absent from the flame
   graph after removing its default registration.
3. SQL transaction commits and TDS/SSL reads dominate the database path and are
   I/O-bound; the trace does not show a materialization CPU hotspot.
4. `ToDataTableArk()` remains negligible at 9.7 ms for 300 single-row calls.

Do not optimize the thread-pool semaphore, Rebus backoff, SQL socket waits, or
Application Insights timer without a CPU-only trace showing application work
behind them.
