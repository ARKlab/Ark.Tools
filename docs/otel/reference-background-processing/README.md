# ReferenceProject background-processing evidence

Captured by one passing test:

```text
ARK_OTEL_FILE_DIRECTORY=/tmp/ark-reference-otel \
ASPNETCORE_ENVIRONMENT=IntegrationTests \
dotnet test Core/Ark.Reference.Core.Tests/Ark.Reference.Core.Tests.csproj \
  --filter 'Name~Print process completes'
```

The run selected the single Reqnroll scenario `Print process completes
successfully in background`. The test waited for the in-memory Rebus bus and
outbox to become idle, then asserted the application span and metrics.

## Files

- `otel-spans.jsonl`: 119 completed spans.
- `otel-metrics.jsonl`: 56 measurements.

The collector deliberately captured every process-local signal. The majority
of spans are SQL client operations (100), with additional ASP.NET Core,
HTTP/Azure client, socket, and security spans. Runtime, HTTP, ASP.NET Core,
Rebus, and application meters are present in the metrics file.

The application-specific records demonstrate the expected result:

- `ark.reference.book_print_process` is a `Consumer` span with
  `book_print_process.id=1`, `book_print_process.status=Completed`, and
  `status=Ok`.
- `ark.reference.book_print_process.progress` records `1` with
  `process.status=Completed`.
- `ark.reference.book_print_process.completed` records `1`.
- `ark.tools.rebus.message_processing_time` records the message duration with
  `operation.result=success`.

Trace and span IDs are retained to allow local correlation between the
application Consumer span and its parent Rebus activity. Values are local test
data; no Azure exporter was configured.
