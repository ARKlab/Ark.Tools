# Adaptive sampling in Ark.Tools

Updated: 2026-10-13

## Decision

`ArkAdaptiveSampler` is a parent-sampling enhancement. It applies an adaptive budget to
roots, then preserves the parent decision for descendants. This is intentionally stricter
than sampling every child independently: a trace is either kept as a chain or retained as
record-only data that can still be promoted when troubleshooting needs it.

## Decision table

| Situation | Decision |
|---|---|
| Pre-filtered successful noise | Drop |
| Root with no parent and available budget | Record and sample |
| Root with no parent and exhausted budget | Record only |
| Local sampled parent | Record and sample |
| Remote sampled parent | Record and sample (budget decision when `RespectRemoteParentSampling` is `false`) |
| Local unsampled parent | Record only |
| Remote unsampled parent | Record only (budget decision when `RespectRemoteParentSampling` is `false`) |
| Failed span or exception | Promote to sampled |
| HTTP 4xx response without error status | Success; do not promote |
| HTTP 4xx response with error status | Promote to sampled |

`RecordOnly` is deliberate. It preserves the activity long enough for the completion
processor to inspect status, exception events, and response codes.

## Failure-first troubleshooting

At completion, `ArkFailurePromotionProcessor`:

1. Treats explicit error status, exception events, HTTP 5xx+, and non-zero gRPC status as
   failures.
2. Registers the trace as failed.
3. Promotes the failing span and all live local ancestors.
4. Promotes siblings that finish after the failure is observed.
5. Causes new descendants to be sampled through the shared failure registry.

Siblings that ended before the failure was observed cannot be recovered. This is the
intentional boundary that avoids retaining every operation indefinitely.

HTTP 4xx codes are not failures by themselves: a 4xx span is promoted only when its status is
`Error`. `WebApi4xxAsSuccessProcessor` runs before failure promotion and clears the error status
for 400-499 server responses. An application processor registered before the Ark setup can clear
the status of expected client errors, such as Azure Storage 404 or 409, in the same way.

## Root rate control

Root spans use a token bucket keyed by operation name. The default burst capacity is two
seconds of tokens, and at least one token. `TracesPerSecond` is the total target for all
buckets. Every `SamplingPercentageDecreaseTimeout`, the moving-average controller measures
the traffic of each bucket and shares the budget max-min fairly: operations slower than the
fair share keep all their roots, and the busier operations split the rest evenly. New and
idle operations start at the current fair share, so the total can exceed the target until
the next adjustment. `MaxOperationBuckets` limits memory when operation names are dynamic;
overflow shares one bucket.

## Sample rate and item counts

Application Insights estimates volumes from the `sampleRate` of each item:
`itemCount = 100 / sampleRate`. Like the Azure Monitor samplers, `ArkAdaptiveSampler`
puts the `microsoft.sample_rate` attribute (0–100) on the spans it samples:

| Span | `microsoft.sample_rate` |
|---|---|
| Root sampled by its bucket | `100 / n`, where `n` counts the root and the roots the bucket skipped since its previous sampled root |
| Local descendant of a sampled span | The parent's rate |
| Span kept because its trace failed, including the failing span, its ancestors and later spans of the trace | `100` (itemCount 1) |
| Span kept only because a remote parent was sampled | None (itemCount 1): the upstream rate is not propagated |

The sum of the item counts of the exported roots therefore matches the number of roots seen.
Failures are kept whatever the budget, so they count only themselves and do not inflate
request or failure counts. The rate is read from the parent `Activity`, so a span started with an
explicit parent context that is not `Activity.Current` or one of its ancestors carries no rate.

`Azure.Monitor.OpenTelemetry.Exporter` 1.10.0 does not read the attribute yet: it sets the
`sampleRate` of every item from `AzureMonitorExporterOptions.SamplingRatio`, so Application
Insights still reports `itemCount = 1` for every sampled span, as it does with the exporter's own
rate-limited sampler. Exporters that read the attribute weight the spans correctly. Until the
.NET exporter does, use the unsampled OpenTelemetry metrics, such as
`http.server.request.duration`, for volumes, rates and count-based alerts.

## Configuration

```json
{
  "ApplicationInsights": {
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100,
      "RespectRemoteParentSampling": true
    }
  }
}
```

`RespectRemoteParentSampling` (default `true`) makes the W3C sampled flag of a remote parent
decide for the service. Set it to `false` when callers use a different sampler, such as the
classic Application Insights SDK, so that their decisions do not drive this service: a span with
a remote parent is then a root for the local budget and carries this service's sample rate, as
with the Azure Monitor samplers. Traces that cross services which decide differently can then
be incomplete; failure promotion applies either way.

## Operational checks

- Verify the root decision before investigating child sampling.
- Verify 4xx normalization before treating missing traces as failures.
- Verify sampled parent continuity across process boundaries.
- Expect only spans that finish after a failure to be retroactively promoted.
- Watch operation-name cardinality and the configured bucket cap.
