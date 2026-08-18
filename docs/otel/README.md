# Ark.Tools OpenTelemetry Integration

Ark.Tools uses **OpenTelemetry** (via Application Insights v3.x) for distributed tracing and telemetry. This document describes the features, configuration, and migration guidance.

---

## Contents

- [Overview](#overview)
- [Features](#features)
- [Getting Started](#getting-started)
- [Configuration Reference](#configuration-reference)
- [Sampling Strategy](#sampling-strategy) → see [sampling.md](sampling.md)
- [Migration from Application Insights v2.x](#migration) → see [applicationinsights-migration/](applicationinsights-migration/)
- [Upgrade guide](upgrade-guide.md)

---

## Overview

Ark.Tools provides an opinionated, cost-efficient telemetry setup built on OpenTelemetry.
Azure Monitor Application Insights is an exporter and compatibility path, not a dependency
of Ark instrumentation. The main goals are:

- **Cost efficiency**: Adaptive sampling keeps telemetry costs predictable
- **Complete error visibility**: Failures are always captured, never dropped
- **Noise reduction**: High-frequency low-value spans are filtered before sampling
- **Per-operation fairness**: Rare code paths get sampled fairly vs. high-frequency ones

---

## Features

### Adaptive Sampling

The `ArkAdaptiveSampler` implements intelligent, cost-efficient sampling:

- **Adaptive rate control**: Dynamically adjusts sampling percentage to hit a target telemetry rate (default: 1 trace/second)
- **Per-operation token buckets**: Each operation (HTTP route, message handler, etc.) gets its own rate budget, ensuring fair representation
- **Failure preservation**: All spans with errors, exceptions, or failed HTTP status codes are **always sampled** regardless of the rate limit

### Pre-filtering (Noise Reduction)

`ArkPreFilterProcessor` drops known-noisy, low-value spans before the sampler sees them:

- `OPTIONS` requests (CORS preflight) – successful only
- Azure Service Bus `Receive` operations – successful only  
- SQL `Commit` operations – successful only
- Empty outbox polling cycles do not create spans
- Optional: specific SQL server/database combinations (for NLog database)

### Telemetry Enrichment

The OTel resource configuration adds process-wide context to every signal:

- `service.name`: The entry assembly name (for multi-process environments)

Application Insights compatibility setup applies this automatically. Exporter-agnostic
applications can compose `ResourceBuilder.AddArkTelemetryResource()` with their
OpenTelemetry resource configuration.

---

## Getting Started

### ASP.NET Core (recommended)

```csharp
builder.Services.AddArkAzureMonitorOpenTelemetry();
```

The extension is provided by `Ark.Tools.AspNetCore.OTel` and uses the Azure Monitor
OpenTelemetry Distro. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` or configure an
alternative exporter in the application.

The Distro also registers an OpenTelemetry logger provider. Ark configures that
provider in code to export only `Error` and `Critical` records; NLog remains
available as a separate logging provider for console, database, mail, and Slack
targets.

### Rebus

```csharp
options.UseOpenTelemetry(container);
options.UseOpenTelemetryMetrics(container);
```

### Existing Application Insights application

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=https://..."
  }
}
```

Register the v3 SDK and the dedicated Ark compatibility package explicitly. Do not rely
on a general Ark hosting package to register it.

### Worker Service / Hosted Service

Use `Ark.Tools.ResourceWatcher.OTel` for ResourceWatcher source and meter registration,
then add the exporter selected by the application. The legacy
`Ark.Tools.ResourceWatcher.ApplicationInsights` package remains available for
applications that still use the SDK.

ResourceWatcher uses the `ark.tools.resourcewatcher` activity source and lowercase,
dot-separated operation names.
The public `ResourceWatcherInstrumentation` constants can be used when configuring a
custom OpenTelemetry provider. Operation payload fields are emitted as span attributes;
exceptions are recorded as exception events and set the span status to error.

### Azure Monitor configuration

The recommended environment variable is:
```
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=https://...
```

`AddArkAzureMonitorOpenTelemetry` also reads
`ApplicationInsights:ConnectionString` from application configuration.

### Core client instrumentations

`AddArkAzureMonitorOpenTelemetry` registers the stable OpenTelemetry .NET
instrumentations for `HttpClient` and `Microsoft.Data.SqlClient`. Flurl clients
created by `Ark.Tools.Http` use `HttpClient`, so their outbound requests are
captured automatically; no Flurl-specific hook is required.

SQL spans keep a compact `db.query.summary` and redact the large
`db.query.text` attribute by default. The SQL client duration meter is enabled
by default. Applications can extend the Ark defaults and opt into query text
only for controlled diagnostics:

```csharp
builder.Services.AddArkAzureMonitorOpenTelemetry(
    builder.Configuration,
    sql => sql.Filter = command => true,
    includeSqlQueryText: true);
```

The default SQL summary includes the target table for `INSERT INTO` commands.

Azure Service Bus uses the tracing `ActivitySource` emitted by the
`Azure.Messaging.ServiceBus` SDK. Ark registers that source
(`Azure.Messaging.ServiceBus`) so sender and receiver spans flow through the
same provider. Because Azure SDK ActivitySource support is experimental,
applications that use Service Bus must enable it before constructing clients:

```csharp
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);
```

Applications should use the current Azure Service Bus SDK rather than adding a
separate, unsupported Service Bus instrumentation package.

The Azure Monitor distro can also register some of these instrumentations.
Applications should verify their exporter setup does not register the same
instrumentation twice.

### Further instrumentation candidates

Prioritize these additions when the corresponding dependency is used:

- **gRPC client** (`OpenTelemetry.Instrumentation.GrpcNetClient`, currently
  beta) for RPC spans and status details. Filter either gRPC or HTTP spans if
  both are enabled to avoid duplicate downstream spans.
- **Entity Framework Core** (`OpenTelemetry.Instrumentation.EntityFrameworkCore`)
  for ORM operation context in addition to SQL spans.
- **StackExchange.Redis** (`OpenTelemetry.Instrumentation.StackExchangeRedis`)
  for cache dependency latency and failures.
- **MongoDB** (`OpenTelemetry.Instrumentation.MongoDB`) or **Npgsql**
  (`OpenTelemetry.Instrumentation.Npgsql`) for applications using those stores.

These should remain opt-in because each adds a provider-specific dependency and
can duplicate lower-level database spans. Review package stability and target
framework support before adding them to the default registration.

---

## Configuration Reference

### Sampling Configuration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "...",
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

| Option | Default | Description |
|--------|---------|-------------|
| `TracesPerSecond` | `1.0` | Target number of traces to export per second (per operation bucket when bucketing is enabled) |
| `MovingAverageRatio` | `0.5` | Smoothing factor for rate adjustment (0 = no smoothing, 1 = no adjustment) |
| `SamplingPercentageDecreaseTimeout` | `00:01:00` | How often to evaluate and adjust the sampling rate |
| `EnablePerOperationBucketing` | `true` | Whether each operation gets its own token bucket |
| `MaxOperationBuckets` | `100` | Maximum distinct operations to track (prevents memory unbounded growth) |

### Snapshot Collector

```json
{
  "SnapshotCollectorConfiguration": {
    "IsEnabled": true,
    "IsEnabledInDeveloperMode": false
  }
}
```

---

## How Sampling Works

See [sampling.md](sampling.md) for a detailed explanation of the adaptive sampling algorithm.

**Short version:**
1. A sampled local or remote parent → **sampled** (the chain stays intact)
2. An unsampled local parent → **RecordOnly** (children cannot contradict the parent)
3. Spans matching noise filters → **dropped immediately**
4. A completed failure → promoted, with its local ancestors and subsequent siblings retained
5. A local root with no parent → adaptive token-bucket decision

---

## Migration

Migrating from Application Insights SDK v2.x? See the [applicationinsights-migration](applicationinsights-migration/) folder for:

- [Migration Analysis](applicationinsights-migration/migration-analysis.md) – architectural changes and impact
- [Implementation Plan](applicationinsights-migration/implementation-plan.md) – what was built and why
- [NuGet Research](applicationinsights-migration/nuget-research.md) – packages considered

---

## Architecture

```
HTTP Request / Message / SQL / etc.
          │
          ▼
  [OpenTelemetry SDK - ActivitySource]
          │ Activity started
          ▼
  [ArkPreFilterProcessor.OnStart]
  Filter noise (OPTIONS, SB Receive, SQL Commit)
          │ (not filtered)
          ▼
  [ArkAdaptiveSampler.ShouldSample]
  • Check: is parent already sampled? → propagate
  • Check: is it pre-filtered? (span tag set by processor) → Drop
  • Check: does an unsampled parent exist? → RecordOnly
  • Check: root per-op token bucket → RecordAndSample or RecordOnly
          │
          ▼
  [ResourceBuilder.AddArkTelemetryResource]
  Add service.name, etc.
          │
          ▼
  [... activity executes ...]
          │
          ▼
  [ArkFailurePromotionProcessor.OnEnd]
  Promote completed failures even if sampler said RecordOnly
          │
          ▼
  [Azure Monitor Exporter → Application Insights]
```

## Local sample diagnostics

The reference integration tests use the optional `ARK_OTEL_FILE_DIRECTORY`
collector to inspect telemetry without sending it to Azure Monitor. The
collector is exporter-free: it starts only when the variable is non-empty,
listens to every process-local `ActivitySource` and `Meter`, and appends JSON
Lines to:

- `otel-spans.jsonl` — completed activities with source, operation, kind,
  trace/span/parent IDs, status, duration, tags, and events.
- `otel-metrics.jsonl` — `long` and `double` measurements with meter,
  instrument, unit, value, and tags.

Azure Monitor remains isolated because `AddArkAzureMonitorOpenTelemetry` calls
`UseAzureMonitor` only when `ApplicationInsights:ConnectionString` or
`APPLICATIONINSIGHTS_CONNECTION_STRING` is non-empty. Keep those values unset
for local diagnostics and tests. Empty Application Insights settings in the
sample integration-test configuration are safe.

### Signal inventory

| Sample | Custom source/meter | Custom spans | Custom metrics |
|---|---|---|---|
| Ark.ReferenceProject | `ark.reference.core.application` | `ark.reference.book_print_process` (`Consumer`), process ID and final status tags | — |
| Ark.ResourceWatcher | `ark.resourcewatcher.sample` | `ark.resourcewatcher.sample.process` (`Internal`), resource ID and record count tags | `ark.resourcewatcher.sample.records_processed` counter; `ark.resourcewatcher.sample.processing_duration` millisecond histogram |
| Ark.MediatorFramework | `ark.mediator.sample.application` | `ark.mediator.sample.book_print_process` (`Consumer`), process ID and final status tags | — |

All three samples also collect Ark framework signals. Rebus uses source/meter
`ark.tools.rebus`; the reference feature specifically asserts
`ark.tools.rebus.message_processing_time` with `operation.result=success`.
Outbox processors use the implementation-independent source/meter
`ark.tools.outbox`. Add `Ark.Tools.Outbox.OTel` and call
`AddArkOutboxOpenTelemetry()` to register those signals. It emits a processing
span only for non-empty batches and records processed messages, batch size, and
processing duration.
ResourceWatcher uses `ark.tools.resourcewatcher` for framework lifecycle
signals. ASP.NET Core samples additionally collect HTTP and SQL client spans
when those instrumentations are used.

### Reference background-processing evidence

Run the single successful Rebus background scenario from
`samples/Ark.ReferenceProject`:

```bash
rm -rf /tmp/ark-reference-otel
ARK_OTEL_FILE_DIRECTORY=/tmp/ark-reference-otel \
ASPNETCORE_ENVIRONMENT=IntegrationTests \
dotnet test Core/Ark.Reference.Core.Tests/Ark.Reference.Core.Tests.csproj \
  --filter "DisplayName~Print process completes successfully in background"
```

The feature waits for the bus and outbox to be idle before asserting the custom
span and successful Rebus processing measurement. A
sanitized committed run is in `docs/otel/reference-background-processing/`:
`otel-spans.jsonl` and `otel-metrics.jsonl`. These files are intentionally
JSONL so they can be searched with `jq`, `grep`, or imported into a notebook.
