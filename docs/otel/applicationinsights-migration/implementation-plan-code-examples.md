# Current OTel extension examples

This document is intentionally limited to the public extension interface currently
supported by `master`. It replaces the former design dump, which described an
older sampler implementation and was not safe to copy into an application.

## ASP.NET Core

```csharp
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
builder.Services.AddAzureMonitorProfiler();
builder.Services.AddArkApplicationInsightsCustomizations(builder.Configuration);
```

Use the overloads exposed by the referenced Ark.Tools package in the application.
Do not copy internal sampler, processor, registry, or exporter implementations.

## Hosted service

```csharp
services.AddApplicationInsightsTelemetryWorkerService(configuration);
services.AddAzureMonitorProfiler();
services.AddArkApplicationInsightsCustomizations(configuration);
```

Keep the Ark.Tools customization call after Application Insights and profiler
registration so the customization can configure the completed telemetry pipeline.

## Current sampling contract

`ArkAdaptiveSampler` enhances parent-based sampling:

1. A recorded local or remote parent keeps every child recorded and sampled.
2. An unsampled local parent keeps children `RecordOnly`, allowing failure promotion
   without independently contradicting the parent's sampling decision.
3. A root span uses the adaptive per-operation budget.
4. A failure promotes the failing span and as much of its local trace as remains
   available.
5. HTTP 4xx responses are normalized as successful before failure promotion.

For the complete decision table and processor ordering, see
[`../sampling.md`](../sampling.md).

## Rebus metrics

```csharp
services.UseApplicationInsightMetrics();
```

The processing step emits processing time for successful and failed messages.
Queue time uses the transport `Headers.SentTime` value and excludes the measured
processing duration. Verify the metric dimensions and success/failure result in
the application's configured Application Insights destination.

## Upgrade and rollout

Follow [`../upgrade-guide.md`](../upgrade-guide.md) for package, registration,
sampling, profiler, Rebus validation, rollback, and locked-restore steps. The
guide is the source of truth for the current extension surface.
