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
