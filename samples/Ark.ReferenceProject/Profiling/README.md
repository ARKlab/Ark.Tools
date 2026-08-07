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
| `LowLevelLifoSemaphore.WaitNative` | 27.38% |
| `WaitHandle.WaitOneNoCheck` | 22.91% |
| `Missing Symbol` | 9.38% |
| `Thread.Sleep` | 7.56% |
| `ManualResetEventSlim.Wait` | 7.37% |
| `LowLevelLifoSemaphore.WaitForSignal` | 6.91% |
| `GC.RunFinalizers` | 5.78% |

The trace is dominated by synchronization, waiting, finalization, and the
intentional progressive-printing delays, not CPU spent in SQL materialization or
`ToDataTableArk()`. Application frames for Rebus, Dapper, and the one-row
`ToDataTableArk()` call are negligible in this workload; larger batches are
needed to measure conversion cost.
