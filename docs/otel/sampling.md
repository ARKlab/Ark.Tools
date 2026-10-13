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
| Remote sampled parent, `RespectRemoteParentSampling` `false` (default) | Budget decision, as a root |
| Remote sampled parent, `RespectRemoteParentSampling` `true` | Record and sample |
| Local unsampled parent | Record only |
| Remote unsampled parent, `RespectRemoteParentSampling` `false` (default) | Budget decision, as a root |
| Remote unsampled parent, `RespectRemoteParentSampling` `true` | Record only |
| Failed span or exception | Promote to sampled |
| HTTP 4xx response without error status | Success; do not promote |
| HTTP 4xx response with error status | Promote to sampled |

`RecordOnly` is deliberate. It preserves the activity long enough for the completion
processor to inspect status, exception events, and response codes.

Only the roots that the bucket logic decides count in the budget: dropped and filtered spans do
not. The sampler applies the pre-filter rules (HTTP `OPTIONS`, SQL commits) and every
`IArkSamplingFilter` (the NLog database filter, and the Service Bus receive filter of
`Ark.Tools.OTel.Azure`) to the span name and creation tags, and drops matching spans before they
reach a bucket. Applications can register their own `IArkSamplingFilter` in the service
collection. Spans that an Ark filter recognizes only after sampling, such as SQL commands
skipped by query label (`outbox.peek-lock` and `SqlQueryLabelsToSkip`) or by an application
SqlClient filter, and spans the pre-filter or NLog processors drop when they start or end, give
back the token and the traffic they counted. Spans dropped after sampling by other processors
or instrumentation filters, such as an application `HttpClient` filter, still count. HTTP `OPTIONS` requests are recognized before sampling when ASP.NET Core passes the request
method at creation: by default from ASP.NET Core 11, and with `AddArkAspNetCoreOpenTelemetry` or
`AddArkAzureMonitorOpenTelemetry` on .NET 10, which set the
`Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData` switch to `false` unless the
application already set it.

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
fair share get a refill rate that covers their average traffic, and the busier operations split
the rest evenly. A burst larger than the bucket is still limited, so a rare operation that sends
many roots at once keeps only the first ones. Idle
operations, and new ones, split the budget the active operations leave unused, so the buckets
stay within the budget when idle operations resume; each still holds one token, so its first
root is sampled. Before the first adjustment every bucket starts at `TracesPerSecond`, and many
operations first seen between two adjustments can also exceed the target until the next one.
`MovingAverageRatio` smooths rate increases only: a bucket above its new
target drops to it at once, so after an adjustment the buckets never share more than the
budget. `MaxOperationBuckets` limits memory when operation names are dynamic;
overflow shares one bucket.

## Sample rate and item counts

Application Insights estimates volumes from the `sampleRate` of each item:
`itemCount = 100 / sampleRate`. Like the Azure Monitor samplers, `ArkAdaptiveSampler`
puts the `microsoft.sample_rate` attribute (0–100) on the spans it samples:

| Span | `microsoft.sample_rate` |
|---|---|
| Root sampled by its bucket | `100 / n`, where `n` counts the root and the roots the bucket skipped since its previous sampled root |
| Local descendant of a sampled span | The parent's rate, copied by `ArkFailurePromotionProcessor` when the span starts |
| Span kept because its trace failed, including the failing span, its ancestors and later spans of the trace | `100` (itemCount 1) |
| Span with a remote parent | As a root; with `RespectRemoteParentSampling`, none (itemCount 1): the upstream rate is not propagated |

Without failures, the sum of the item counts of the exported roots matches the number of roots
seen. The roots a bucket skipped are counted when it samples its next root, so short time
windows lag slightly, and the roots skipped after an operation's last sampled root are never
counted. Failures are kept whatever the budget, so they count only themselves. A promoted root
is also counted in its bucket's next sampled root, and a sampled root that fails no longer
stands for the roots skipped before it; sampling does not depend on the outcome, so these
effects cancel out on average and the sum stays an unbiased estimate. The rate is copied from `Activity.Parent`, so a span started with an
explicit parent context carries no rate.

`Azure.Monitor.OpenTelemetry.Exporter` 1.10.0 does not read the attribute yet: it sets the
`sampleRate` of every item from `AzureMonitorExporterOptions.SamplingRatio`, so Application
Insights still reports `itemCount = 1` for every sampled span, as it does with the exporter's own
rate-limited sampler. Exporters that read the attribute weight the spans correctly. Until the
.NET exporter does, use the unsampled OpenTelemetry metrics, such as
`http.server.request.duration`, for volumes, rates and count-based alerts.

## Configuration

```json
{
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

By default (`RespectRemoteParentSampling` is `false`) a span with a remote parent is a root for
the local budget and carries this service's sample rate, as with the Azure Monitor samplers, so
callers that use a different sampler, such as the classic Application Insights SDK, do not
decide for the service. Traces that cross services which decide differently can then be
incomplete; failure promotion applies either way. Set it to `true` to follow the W3C sampled
flag of the remote parent instead: such spans then carry no sample rate (itemCount 1).

`OTel:ArkAdaptiveSampler` is the canonical section for the OpenTelemetry setup
(`AddArkOpenTelemetry` when given an `IConfiguration`, `AddArkAzureMonitorOpenTelemetry`
and `AddArkOpenTelemetryForWorkerHost`). `AddArkAzureMonitorOpenTelemetry` also reads
the legacy `ApplicationInsights:ArkAdaptiveSampler` section for compatibility; values
in `OTel:ArkAdaptiveSampler` take precedence. The Application Insights v3 compatibility path
(`AddArkApplicationInsightsCustomizations`) reads only
`ApplicationInsights:ArkAdaptiveSampler`.

## Operational checks

- Verify the root decision before investigating child sampling.
- Verify 4xx normalization before treating missing traces as failures.
- Verify sampled parent continuity across process boundaries.
- Expect only spans that finish after a failure to be retroactively promoted.
- Watch operation-name cardinality and the configured bucket cap.
