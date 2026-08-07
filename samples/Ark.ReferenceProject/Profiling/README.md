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
described in the parent project README. Then run:

```bash
dotnet build Ark.Reference.slnx --configuration Release
dotnet tool install --global dotnet-trace
dotnet-trace collect --output artifacts/reference-profile.nettrace \
  -- dotnet run --project Profiling/Ark.Reference.Profiling.csproj \
  --configuration Release --no-build -- --warmup 10 --iterations 100
dotnet-trace report artifacts/reference-profile.nettrace topN -n 20
```

The profiling host deploys `Ark.Reference.Core.Database.dacpac` before starting
the application, matching the C# deployment used by the integration tests.

The trace is intentionally generated under `artifacts/` and is not committed.

## Trace summary

The workload was designed to make framework and application CPU visible while
keeping database and message-processing work representative. Analyze the
`topN` report with the SQL and Rebus categories separated from ASP.NET pipeline
overhead. The expected dominant application frames are SQL materialization and
serialization; `ToDataTableArk()` is only a meaningful hotspot when the workload
contains large batches, so the single-row call is a coverage check rather than a
benchmark.
