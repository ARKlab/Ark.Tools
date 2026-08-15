# Adaptive sampling in Ark.Tools

Updated: 2026-08-15

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
| Remote sampled parent | Record and sample |
| Local unsampled parent | Record only |
| Remote unsampled parent | Record only |
| Failed span or exception | Promote to sampled |
| HTTP 4xx response | Success; do not promote |

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

HTTP 4xx responses are expected API outcomes. `WebApi4xxAsSuccessProcessor` must run before
failure promotion and clear the error status for 400-499 responses.

## Root rate control

Root spans use a token bucket keyed by operation name. The default burst capacity is two
seconds of tokens. `TracesPerSecond` controls the target, while the moving-average
controller adjusts the rate periodically using observed traffic. `MaxOperationBuckets`
limits memory when operation names are dynamic; overflow shares one bucket.

## Configuration

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

## Operational checks

- Verify the root decision before investigating child sampling.
- Verify 4xx normalization before treating missing traces as failures.
- Verify sampled parent continuity across process boundaries.
- Expect only spans that finish after a failure to be retroactively promoted.
- Watch operation-name cardinality and the configured bucket cap.
