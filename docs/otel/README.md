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

---

## Overview

Ark.Tools provides an opinionated, cost-efficient telemetry setup built on top of Application Insights SDK v3 (OpenTelemetry-based). The main goals are:

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

`ArkTelemetryEnrichmentProcessor` adds context to every span:

- `ProcessName`: The entry assembly name (for multi-process environments)

---

## Getting Started

### ASP.NET Core

```csharp
// Program.cs or Startup.cs
builder.Host.AddApplicationInsithsTelemetryForWebHostArk();

// Or via services:
services.ArkApplicationInsightsTelemetry(configuration);
```

### Worker Service / Hosted Service

```csharp
builder.AddApplicationInsightsForHostedService();
```

### Required Configuration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=https://..."
  }
}
```

Or via environment variable:
```
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=https://...
```

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
1. Spans for errors/exceptions → **always exported** (RecordAndSample)
2. Spans matching noise filters → **dropped immediately** (processor returns before sampler)
3. Successful spans → token bucket per operation; if bucket has capacity → export; else → drop
4. Token buckets refill at `TracesPerSecond` rate and adjust adaptively to observed traffic

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
  • Check: error/exception? → RecordAndSample (always)
  • Check: per-op token bucket → RecordAndSample or Drop
          │
          ▼
  [ArkTelemetryEnrichmentProcessor.OnStart]
  Add ProcessName, etc.
          │
          ▼
  [... activity executes ...]
          │
          ▼
  [ArkAdaptiveSampler: OnEnd via ParentBased wrapper]
  Force RecordAndSample on completed failures even if sampler said Drop
          │
          ▼
  [Azure Monitor Exporter → Application Insights]
```
