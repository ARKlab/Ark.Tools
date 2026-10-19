# Ark.Tools OpenTelemetry Integration

Ark.Tools uses **OpenTelemetry** (via Application Insights v3.x) for distributed tracing and telemetry. This document describes the features, configuration, and migration guidance.

---

## Contents

- [Overview](#overview)
- [Features](#features)
- [Getting Started](#getting-started)
- [Configuration Reference](#configuration-reference)
- [Instrumentation Reference](instrumentation-reference.md)
- [Sampling Strategy](#sampling-strategy) → see [sampling.md](sampling.md)
- [Migration from Application Insights v2.x](#migration) → see [../design/otel/applicationinsights-migration/migration-analysis.md](../design/otel/applicationinsights-migration/migration-analysis.md)
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

- **Adaptive rate control**: Dynamically adjusts sampling to hit a total target rate of root traces (default: 1 trace/second)
- **Per-operation token buckets**: Each operation (HTTP route, message handler, etc.) gets a fair share of the budget, so rare operations are still represented
- **Failure preservation**: All spans that end with `Error` status are **always sampled** regardless of the rate limit; the instrumentation sets it for HTTP 5xx responses, exceptions and failed dependencies

### Pre-filtering (Noise Reduction)

`ArkAdaptiveSampler` drops known-noisy, low-value spans before sampling, from their name and
creation tags, so they do not use its budget; `ArkPreFilterProcessor` drops the ones recognized
only after they start:

- `OPTIONS` requests (CORS preflight) – successful only
- SQL `Commit` operations – successful only
- Azure Service Bus `Receive` operations, through the `Ark.Tools.OTel.Azure` sampling filter
- Empty outbox polling cycles do not create spans
- Outbox SQL polling commands are filtered by default
- Optional: specific SQL server/database combinations (for NLog database)

### Telemetry Enrichment

The OTel resource configuration adds process-wide context to every signal:

- `service.name`: The entry assembly name (for multi-process environments)
- `service.version`: The entry assembly informational version, commit included
  (for example `1.2.3+abc123`), or else its assembly version. Azure Monitor shows it
  as `application_Version`.

`OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` override both.

Application Insights compatibility setup applies this automatically. Exporter-agnostic
applications can compose `ResourceBuilder.AddArkTelemetryResource()` with their
OpenTelemetry resource configuration.

---

## Getting Started

### ASP.NET Core (recommended)

```csharp
builder.Services.AddOpenTelemetry()
    .AddArkAspNetCoreOpenTelemetry(configuration: builder.Configuration)
    .AddArkAzureOpenTelemetry(builder.Configuration);
```

`AddArkAspNetCoreOpenTelemetry` (`Ark.Tools.AspNetCore.OTel`) adds the Ark pipeline and the
ASP.NET Core instrumentation only. A host running on Azure also calls
`AddArkAzureOpenTelemetry` (`Ark.Tools.OTel.Azure`), as a worker does: it adds Azure SDK tracing
and Azure resource detection, and exports to Azure Monitor with
`Azure.Monitor.OpenTelemetry.Exporter` when `APPLICATIONINSIGHTS_CONNECTION_STRING` or
`ApplicationInsights:ConnectionString` is set. Other exporters can be added instead. Ark does not use
the Azure Monitor OpenTelemetry Distro: unlike the Distro, Ark keeps the OpenTelemetry redaction of URL query strings, and
does not add the Application Insights profiler tagging or the Azure SDK log forwarder.

The Azure Monitor exporter also registers an OpenTelemetry logger provider. Ark configures that
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

Call `AddArkOpenTelemetry()` from `Ark.Tools.OTel` on the OpenTelemetry builder. It
registers the same sampler, processors and client instrumentations as the ASP.NET Core
setup, without the ASP.NET Core dependency. For a ResourceWatcher worker,
`AddArkOpenTelemetryForWorkerHost()` from `Ark.Tools.ResourceWatcher.OTel` calls it and
adds the ResourceWatcher source and meter. Then add the exporter selected by the
application. A worker that uses Azure SDKs or exports to Azure Monitor adds
`AddArkAzureOpenTelemetry(configuration)` from `Ark.Tools.OTel.Azure` (see below). The legacy
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

`AddArkAzureOpenTelemetry` also reads
`ApplicationInsights:ConnectionString` from application configuration.

### Core client instrumentations

`AddArkOpenTelemetry`, which `AddArkAspNetCoreOpenTelemetry` and
`AddArkOpenTelemetryForWorkerHost` call, registers the
stable OpenTelemetry .NET instrumentations for `HttpClient` and
`Microsoft.Data.SqlClient`. Flurl clients created by `Ark.Tools.Http` use
`HttpClient`, so their outbound requests are captured automatically; no
Flurl-specific hook is required. It also collects `HttpClient` metrics
(`System.Net.Http`), .NET runtime and process metrics (`System.Runtime`) and the
Outbox source and meter. `AddArkAspNetCoreOpenTelemetry` adds ASP.NET Core server
spans and request metrics (`Microsoft.AspNetCore.Hosting`).

A few of these instruments cost storage without helping diagnostics, so Ark does
not collect them: `http.client.open_connections`, `http.client.active_requests`,
`dotnet.gc.last_collection.heap.fragmentation.size`, `dotnet.process.cpu.count`,
`dotnet.assembly.count`, `dotnet.thread_pool.work_item.count` and `dotnet.jit.*`.
They are listed in `ArkOtelConfig.DroppedMetrics`, which accepts `*` and `?`
wildcards; remove a name to collect that metric again, or add one to drop more:

```csharp
builder.Services.AddOpenTelemetry()
    .AddArkAspNetCoreOpenTelemetry(
        configureArkOtel: otel => otel.DroppedMetrics.Remove("dotnet.jit.*"),
        configuration: builder.Configuration);
```

When given configuration, `AddArkOpenTelemetry` drops the SQL spans of NLog's own
database writes, matching the `ConnectionStrings:NLog.Database` server and database.

SQL spans redact the large `db.query.text` attribute by default. A command can
carry a `-- otel-query-label: ...` linting comment; its trimmed, sanitized value
is emitted as `db.query.label`. Commands with the `outbox.peek-lock` label
are skipped by default; applications can add more labels to skip. The SQL
client duration meter is enabled by default. Applications can extend the Ark
defaults and opt into query text only for controlled diagnostics:

```csharp
builder.Services.AddOpenTelemetry()
    .AddArkAspNetCoreOpenTelemetry(
        sql => sql.Filter = command => true,
        configureArkOtel: otel =>
        {
            otel.IncludeSqlQueryText = true;
            otel.SqlQueryLabelsToSkip = ["health-check"];
        },
        configuration: builder.Configuration)
    .AddArkAzureOpenTelemetry(builder.Configuration);
```

The SQL Server outbox polling command uses this label instead of matching SQL
hints, so unrelated commands containing hints remain visible.

Everything Azure-specific lives in `Ark.Tools.OTel.Azure`, for any host:

- `AddArkAzureSdkInstrumentation()` collects Azure SDK spans, Azure Service Bus included,
  through the `Azure.*` sources; like the Azure Monitor Distro, it drops the `HttpClient` span
  nested under an Azure SDK span, so a call is not reported twice, and an application
  `HttpClient` filter still applies. Its sampling filter drops Service Bus receives before they
  use the sampler budget.
- `AddArkAzureOpenTelemetry(configuration)` adds it to an Ark setup, with the Azure App Service,
  Virtual Machine and Container Apps resource detectors (instance ID, region, resource ID; the
  entry assembly name stays `service.name`, so processes sharing a site stay apart), and, when an Application Insights
  connection string is configured, exports traces, metrics and error logs to Azure Monitor with
  `Azure.Monitor.OpenTelemetry.Exporter`. Use it in workers:
  `services.AddOpenTelemetry().AddArkOpenTelemetry(configuration: configuration).AddArkAzureOpenTelemetry(configuration)`.
- ASP.NET Core hosts call it after `AddArkAspNetCoreOpenTelemetry`, see
  [ASP.NET Core](#aspnet-core-recommended).

Azure SDK tracing for Service Bus and Event Hubs is experimental and needs the
`Azure.Experimental.EnableActivitySource` switch, which Azure.Core reads only once, when the
first Azure SDK client starts. The `Ark.Tools.OTel.Azure` package therefore sets it in the
application's runtime configuration, before Key Vault or App Configuration clients can start
(set the MSBuild property `ArkEnableAzureSdkActivitySource` to `false` to opt out). Azure.Core
reads that switch before the `AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE` environment variable,
so the variable has no effect in an application that references the package; use the MSBuild
property instead. With a project reference, which does not import the props file,
`AddArkAzureSdkInstrumentation()` sets the switch unless the application or the environment
variable already did.
The other setups do not collect Azure SDK spans.

Applications should use the current Azure Service Bus SDK rather than adding a
separate, unsupported Service Bus instrumentation package.

Applications that add their own exporter setup, such as the Azure Monitor Distro, should
verify it does not register the same instrumentation twice.

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
    "ConnectionString": "..."
  },
  "OTel": {
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100,
      "RespectRemoteParentSampling": false
    }
  }
}
```

The Azure Monitor connection string stays under `ApplicationInsights`. The sampler
reads `OTel:ArkAdaptiveSampler`; `AddArkAzureOpenTelemetry` also reads the
legacy `ApplicationInsights:ArkAdaptiveSampler` section for compatibility, and
`OTel` values take precedence. The
Application Insights v3 compatibility path (`AddArkApplicationInsightsCustomizations`)
reads only `ApplicationInsights:ArkAdaptiveSampler`.

| Option | Default | Description |
|--------|---------|-------------|
| `TracesPerSecond` | `1.0` | Target number of root traces to export per second, in total across operation buckets |
| `MovingAverageRatio` | `0.5` | Smoothing factor for rate increases (0 = no smoothing, 1 = rates never increase); decreases apply at once |
| `SamplingPercentageDecreaseTimeout` | `00:01:00` | How often to evaluate and adjust the sampling rate |
| `EnablePerOperationBucketing` | `true` | Whether each operation gets its own token bucket and a max-min fair share of `TracesPerSecond` |
| `MaxOperationBuckets` | `100` | Maximum distinct operations to track (prevents memory unbounded growth) |
| `RespectRemoteParentSampling` | `false` | Whether the sampled flag of a remote parent decides for the service; `false` applies the local budget to spans with a remote parent. It applies to every remote parent of the host: incoming HTTP requests and incoming Rebus and MediatorFramework messages |

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
1. A sampled local parent → **sampled** (the chain stays intact); a remote parent is ignored unless `RespectRemoteParentSampling` is `true`
2. An unsampled local parent → **RecordOnly** (children cannot contradict the parent)
3. Spans matching noise filters → **dropped immediately**
4. A completed failure → promoted, with its local ancestors and subsequent siblings retained
5. A local root with no parent → adaptive token-bucket decision

---

## Migration

Migrating from Application Insights SDK v2.x? Start with the
[upgrade guide](upgrade-guide.md) and the
[migration summary](../design/otel/applicationinsights-migration/migration-analysis.md).

---

## Architecture

```
HTTP Request / Message / SQL / etc.
          │
          ▼
  [OpenTelemetry SDK - ActivitySource]
          │ Activity created
          ▼
  [ArkAdaptiveSampler.ShouldSample]
  • Check: is it filtered? (name and creation tags: OPTIONS, SQL Commit,
    ArkAdaptiveSamplerOptions.Filter such as NLog database and Service Bus receive) → Drop, no budget used
  • Check: is parent already sampled? → propagate
  • Check: does an unsampled parent exist? → RecordOnly
  • Check: root per-op token bucket → RecordAndSample or RecordOnly
          │ Activity started
          ▼
  [ArkPreFilterProcessor.OnStart]
  Filter noise recognized only after start; give the budget back
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

Azure Monitor remains isolated because `AddArkAzureOpenTelemetry` adds the
Azure Monitor exporter only when `ApplicationInsights:ConnectionString` or
`APPLICATIONINSIGHTS_CONNECTION_STRING` is non-empty. Keep those values unset
for local diagnostics and tests. Empty Application Insights settings in the
sample integration-test configuration are safe.

### Signal inventory

| Sample | Custom source/meter | Custom spans | Custom metrics |
|---|---|---|---|
| Ark.ReferenceProject | `ark.reference.core.application` | `ark.reference.book_print_process` (`Consumer`), process ID and final status tags | — |
| Ark.ResourceWatcher | `ark.resourcewatcher.sample` | `ark.resourcewatcher.sample.process` (`Internal`), resource ID and record count tags | `ark.resourcewatcher.sample.records_processed` counter; `ark.resourcewatcher.sample.processing_duration` millisecond histogram |
| Ark.Tools.MediatorFramework | `ark.mediator.sample.application` | `ark.mediator.sample.book_print_process` (`Consumer`), process ID and final status tags | — |

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
