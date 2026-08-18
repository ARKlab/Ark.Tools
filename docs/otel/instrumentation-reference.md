# Ark.Tools OpenTelemetry reference

This document is the reference for the instrumentation and sampling behavior
implemented by Ark.Tools. It describes registrations, emitted signals, default
settings, and attributes. Provider-generated attributes follow the OpenTelemetry
semantic conventions for the provider version in use.

## Setup profiles

### ASP.NET Core Azure Monitor OTel

Registration:

```csharp
builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);
```

Implemented by `Ark.Tools.AspNetCore.OTel`.

| Signal | Default registration | Notes |
|---|---|---|
| Resource | `AddArkTelemetryResource()` | Sets `service.name` to the entry assembly name. |
| Traces | `ark.tools.rebus` source | The source must also be used by Rebus through `UseOpenTelemetry`. |
| Traces | `HttpClient` instrumentation | Captures outbound `HttpClient` requests, including Flurl clients built on `HttpClient`. |
| Traces | `Microsoft.Data.SqlClient` instrumentation | SQL spans; `RecordException = true`. |
| Traces | `Azure.Messaging.ServiceBus` source | Captures SDK spans when the Azure experimental ActivitySource switch is enabled before client creation. |
| Metrics | `ark.tools.rebus` meter | Rebus metrics are emitted only when `UseOpenTelemetryMetrics` is installed in the Rebus pipeline. |
| Metrics | SQL Client instrumentation | Provider SQL client duration metrics. |
| Logs | OpenTelemetry logger provider | Only `Error` and `Critical` records are enabled for OTel export. |

Azure Monitor is configured only when
`ApplicationInsights:ConnectionString`, `APPLICATIONINSIGHTS_CONNECTION_STRING`
in configuration, or the environment variable contains a value. Without a
connection string the setup remains exporter-neutral.

### Exporter-neutral Rebus

Registration in Rebus:

```csharp
options.UseOpenTelemetry(container);
options.UseOpenTelemetryMetrics(container);
```

The extensions install the Rebus pipeline steps. Adding the
`ark.tools.rebus` source or meter alone does not create signals.

### Exporter-neutral Outbox

Registration:

```csharp
builder.Services.AddOpenTelemetry()
    .AddArkOutboxOpenTelemetry();
```

Implemented by `Ark.Tools.Outbox.OTel`. This registration is opt-in and is
independent of Rebus. The signals are emitted by `OutboxProcessorBase`, so
other outbox implementations can use the same contract.

### Exporter-neutral ResourceWatcher

Registration:

```csharp
builder.Services.AddOpenTelemetry()
    .AddArkResourceWatcherOpenTelemetry();
```

`AddArkOpenTelemetryForWorkerHost` is the equivalent worker-host convenience
extension. This registration is opt-in.

## Instrumentation contracts

### HTTP client

Registration: `AddHttpClientInstrumentation()`.

| Signal | Name/source | Attributes |
|---|---|---|
| Span | Provider `HttpClient` instrumentation | Provider-generated HTTP client semantic-convention attributes, such as method, URL/route, status, server address/port, and error details. |
| Meter | None registered by `AddArkAspNetCoreOpenTelemetry` | Add a provider metrics registration separately if HTTP client metrics are required. |

Ark.Tools does not add a custom HTTP span or meter.

### Microsoft SQL Server client

Registration: `AddSqlClientInstrumentation()` for tracing and metrics.

| Signal | Name/source | Attributes |
|---|---|---|
| Span | Provider SQL Client instrumentation | Provider-generated database attributes, including current `db.system.name`, `db.namespace`, `server.address`, and query attributes when available. |
| Meter | Provider SQL Client instrumentation | Provider SQL client duration instrument and its provider-defined database attributes. |

Ark.Tools adds the following span behavior:

| Behavior | Default |
|---|---|
| Exception recording | Enabled (`RecordException = true`). |
| `db.query.text` | Removed on span completion. Set `includeSqlQueryText: true` only for controlled diagnostics. |
| Query label | A `-- otel-query-label: <label>` SQL comment adds the sanitized label as `db.query.label`. |
| Skipped labels | `outbox.peek-lock` is skipped by default. Additional labels can be supplied through `sqlQueryLabelsToSkip`. |
| Application filter | Composed with the Ark filter; both filters must accept a command for its span to be created. |

`ArkSqlClientSpanProcessor` performs query-label extraction and query-text
redaction on span completion, after the provider has populated database tags.
`ArkSqlDependencyFilterProcessor` is a separate optional processor for the
Application Insights customization path. It matches SQL Server spans by:

```text
db.system.name = microsoft.sql_server
db.namespace   = <Initial Catalog>
server.address = <Data Source host>
```

It also accepts older SQL system values and equivalent legacy tags. A configured
`Data Source` containing a port is compared by host, so `localhost,1433`
matches `server.address = localhost`.

### Rebus

Source and meter: `ark.tools.rebus`.

#### Consumer span

| Field | Value |
|---|---|
| Name | `ark.tools.rebus.process` |
| Kind | `Consumer` |
| Tags | `messaging.system=rebus`, `messaging.operation.type=process`, `messaging.message.id`, `messaging.message.type`, `message.type`, `rebus.correlation_id` |
| Failure | `Status=Error` and an `exception` event with `exception.type`, `exception.message`, and `exception.stacktrace` |

#### Metrics

| Instrument | Unit | Attributes | Emission |
|---|---|---|---|
| `ark.tools.rebus.message_time_in_queue_success` | `ms` histogram | `message.type` | Success only; invalid/missing sent-time headers produce no queue-time measurement. |
| `ark.tools.rebus.message_processing_time` | `ms` histogram | `message.type`, `operation.result` | Always; `operation.result` is `success` or `failure`. |

Outgoing Rebus messages propagate the current W3C activity ID in the
`Diagnostic-Id` header.

### Outbox

Source and meter: `ark.tools.outbox`.

#### Batch span

| Field | Value |
|---|---|
| Name | `ark.tools.outbox.process` |
| Kind | `Producer` |
| Tags | `messaging.system=outbox`, `messaging.operation.type=process`, `outbox.batch.size` |
| Failure | `Status=Error` and an `exception` event with standard exception tags |

Empty polling cycles do not create a span. A span covers a non-empty batch,
including message processing and context commit.

#### Metrics

| Instrument | Unit | Attributes | Meaning |
|---|---|---|---|
| `ark.tools.outbox.messages.processed` | `{message}` counter | `operation.result` | Number of messages in a processed batch. |
| `ark.tools.outbox.batch.size` | `{message}` histogram | `operation.result` | Number of messages retrieved by a poll. |
| `ark.tools.outbox.processing.duration` | `s` histogram | `operation.result` | Batch processing and commit duration. |

`operation.result` is `success` or `failure`. Batch size is not queue depth;
it does not add a database queue-depth query.

### ResourceWatcher

Source and meter: `ark.tools.resourcewatcher`.

All activity names are lowercase and dot-separated. The activity payload is
mapped to snake_case span attributes. `TimeSpan` values are emitted as
milliseconds and enum values as strings.

| Span | Kind | Attributes |
|---|---|---|
| `ark.tools.resourcewatcher.run` | `Internal` | `type`, `now`, `tenant`, then `resources_found`, result counts, and `elapsed` at completion |
| `ark.tools.resourcewatcher.get_resources` | `Internal` | `resources_found`, `elapsed`, `tenant` |
| `ark.tools.resourcewatcher.check_state` | `Internal` | `resources_new`, `resources_updated`, `resources_retried`, `resources_retried_after_ban`, `resources_banned`, `resources_nothing_to_do`, `tenant` |
| `ark.tools.resourcewatcher.process_resource` | `Internal` | `resource_id`, `index`, `total`, `process_type`, `modified_source`, `current_modified`, `tenant`, and `result_type` at completion |
| `ark.tools.resourcewatcher.fetch_resource` | `Internal` | `resource_id`, `index`, `total`, `process_type`, `tenant` |

Failures set `Status=Error` and add an `exception` event. ResourceWatcher also
retains its legacy diagnostic listener for non-OTel consumers.

| Instrument | Unit | Attributes |
|---|---|---|
| `ark.tools.resourcewatcher.runs` | counter | `outcome`: `success` or `failed` |
| `ark.tools.resourcewatcher.resources.listed` | counter | None |
| `ark.tools.resourcewatcher.resources.processed` | counter | `outcome`: `success`, `no_new_data`, `no_action`, `skip`, `banned`, or `failed` |

### Azure Service Bus

Ark.Tools registers the Azure SDK source
`Azure.Messaging.ServiceBus`; the SDK creates the producer/consumer spans.
Ark.Tools does not add a custom meter or custom tags. Enable
`Azure.Experimental.EnableActivitySource` before constructing Service Bus
clients. Provider-generated messaging attributes and propagation follow the
Azure SDK and OpenTelemetry semantic conventions.

## Adaptive sampler

The `ArkAdaptiveSampler` is a parent-sampling enhancement. It is not installed
by `AddArkAzureMonitorOpenTelemetry`; Azure Monitor's OTel distro owns sampling
for that setup. It is installed by
`AddArkApplicationInsightsCustomizations`, after registering the Application
Insights v3 SDK.

The sampler and `ArkFailurePromotionProcessor` must share one
`FailedTraceRegistry` for whole-operation failure promotion.

### Decision order

| Condition | Decision |
|---|---|
| Span marked `ark.filtered=true` | `Drop` |
| Parent is recorded | `RecordAndSample` |
| Parent exists but is not recorded | `RecordOnly` |
| Trace is already registered as failed | `RecordAndSample` |
| Root bucket has tokens | `RecordAndSample` |
| Root bucket is empty | `RecordOnly` |

`RecordOnly` is intentional: the span remains available to completion
processors. It can be promoted if it fails.

### Completion and failure promotion

`ArkFailurePromotionProcessor` promotes a `RecordOnly` span when any of these
conditions is true:

- `ActivityStatusCode.Error`
- an event named `exception`
- `http.response.status_code >= 400`
- non-zero `rpc.grpc.status_code`

The processor promotes the failing span and live local ancestors, registers the
trace as failed, and promotes siblings or descendants that complete after the
failure is observed. Siblings already completed cannot be recovered.

`WebApi4xxAsSuccessProcessor` clears the error status for HTTP 400–499 server
spans and must run before failure promotion when both are configured.

### Rate control

Each operation name gets a thread-safe token bucket by default. A bucket starts
with a two-second burst capacity. The adaptive controller periodically adjusts
the refill rate from observed traffic and the configured target. Dynamic
operation names are bounded by `MaxOperationBuckets`; overflow shares the
`__overflow__` bucket. Disabling per-operation bucketing uses `__global__`.

### Options

Configuration section:
`ApplicationInsights:ArkAdaptiveSampler`.

| Option | Default | Effect |
|---|---:|---|
| `TracesPerSecond` | `1.0` | Target root traces per second per operation bucket. |
| `MovingAverageRatio` | `0.5` | Rate smoothing: `0` adapts immediately; `1` prevents adaptation. |
| `SamplingPercentageDecreaseTimeout` | `00:01:00` | Adaptive controller interval. |
| `EnablePerOperationBucketing` | `true` | Gives each operation its own budget. |
| `MaxOperationBuckets` | `100` | Maximum distinct operation buckets before overflow sharing. |

Example:

```json
{
  "ApplicationInsights": {
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100
    }
  }
}
```

## Source reference

| Contract | Source |
|---|---|
| ASP.NET Core setup | `src/aspnetcore/Ark.Tools.AspNetCore.OTel/Ex.cs` |
| SQL redaction and labels | `src/common/Ark.Tools.OTel/ArkSqlClientSpanProcessor.cs`, `ArkSqlQueryLabel.cs` |
| SQL target filtering | `src/common/Ark.Tools.OTel/ArkSqlDependencyFilterProcessor.cs` |
| Adaptive sampling | `src/common/Ark.Tools.OTel/ArkAdaptiveSampler.cs`, `ArkAdaptiveSamplerOptions.cs` |
| Failure promotion and pre-filtering | `src/common/Ark.Tools.OTel/ArkFailurePromotionProcessor.cs`, `ArkPreFilterProcessor.cs` |
| Rebus | `src/common/Ark.Tools.Rebus/OpenTelemetryStep.cs`, `OpenTelemetryProcessingMetricsStep.cs` |
| Outbox | `src/common/Ark.Tools.Outbox/OutboxProcessorBase.cs`, `src/common/Ark.Tools.Outbox.OTel/Ex.cs` |
| ResourceWatcher | `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcherDiagnosticSource.cs`, `src/resourcewatcher/Ark.Tools.ResourceWatcher.OTel/Ex.cs` |
