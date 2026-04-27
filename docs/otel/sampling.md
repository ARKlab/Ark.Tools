# Adaptive Sampling in Ark.Tools

## Why Adaptive Sampling?

Telemetry costs scale with volume. A busy service can emit thousands of spans per second; exporting every span to Application Insights would be prohibitively expensive. Traditional **fixed-rate sampling** (e.g., keep 1% of traces) has two problems:

1. **It can miss rare failures** – if only 1 in 10,000 requests fail, you'd need a very high sampling rate to reliably see failures
2. **It over-samples common paths, under-samples rare ones** – a health-check endpoint that runs 100×/second saturates the budget; an admin endpoint called once an hour may never be sampled

The Ark.Tools adaptive sampler solves both problems.

---

## Goals

| Goal | Mechanism |
|------|-----------|
| Always capture errors & exceptions | Failure preservation logic in sampler |
| Keep costs predictable | Token-bucket rate limiter per operation |
| Fair sampling of rare vs. frequent paths | Per-operation token buckets |
| Smooth rate adaptation to traffic changes | Adaptive rate controller with moving average |
| Reduce noise | Pre-filter processor removes known-useless spans |

---

## How It Works

### 1. Pre-filtering (before sampling decision)

`ArkPreFilterProcessor.OnStart` marks certain spans as "filter" before the sampler sees them:

- Successful `OPTIONS` requests (CORS preflight noise)
- Successful Azure Service Bus `Receive` spans (very high frequency, no diagnostic value)
- Successful SQL `Commit` spans
- Optionally, SQL spans to a specific NLog database

Filtered spans are marked via `activity.IsAllDataRequested = false` and `activity.ActivityTraceFlags` stripped of `Recorded` flag, so the SDK stops collecting data immediately.

### 2. Sampler decision (`ShouldSample`)

For every new span the `ArkAdaptiveSampler` evaluates:

```
if span is pre-filtered → Drop
if parent span is already sampled → RecordAndSample  (propagate parent decision)
if span has error/exception indicators → RecordAndSample  (always keep failures)
else → token bucket for this operation
    if bucket has capacity → RecordAndSample + consume token
    else → RecordOnly  (record data but don't export, so OnEnd can still check)
```

Using `RecordOnly` (instead of `Drop`) for rate-limited spans is important: it means the Activity is still created and data is still collected, so the `OnEnd` processor can check if the span *ended* as a failure and promote it to `RecordAndSample`.

### 3. Post-completion failure promotion (`OnEnd` via processor)

`ArkFailurePromotionProcessor` runs in the `OnEnd` pipeline (after the span is fully populated but before the exporter picks it up). It implements **whole-operation failure promotion**:

```
for every span in OnEnd:
  if span is already sampled (Recorded flag set):
      if IsFailure(span) → register TraceId in FailedTraceRegistry
      return (already going to be exported)

  if IsFailure(span):
      register TraceId in FailedTraceRegistry   ← sibling/future spans will see this
      promote span to Recorded
      walk activity.Parent chain upward:
          for each in-process parent span (not yet ended):
              if not Recorded → promote to Recorded
              (parent spans are guaranteed to still be alive here because
               children always end before their parent in a single-process trace)

  else if FailedTraceRegistry.IsFailed(span.TraceId):
      promote span to Recorded
      (this catches in-flight siblings that complete after the failure was detected)
```

The shared **`FailedTraceRegistry`** links the processor back to the sampler:

```
ArkAdaptiveSampler.ShouldSample():
    ...
    if FailedTraceRegistry.IsFailed(samplingParameters.TraceId):
        return RecordAndSample   ← new child spans after failure detection always sampled
```

#### What gets captured when a span fails

| Span | Captured? | How |
|------|-----------|-----|
| The failing span itself | ✅ Always | Promoted in `OnEnd` |
| All in-process parent/ancestor spans | ✅ Always | Parent-chain walk in `OnEnd`; parents haven't ended yet |
| Sibling/child spans that end **after** the failure is detected | ✅ Always | Registry check in `OnEnd` and `ShouldSample` |
| Sibling spans that ended **before** the failure is detected | ❌ Not possible | Already processed by the export pipeline |

In practice, the most important span to always capture is the **root operation span** (e.g., the top-level HTTP request handler). Because children always end before their parent, the root span is guaranteed to be in the parent chain and will always be promoted.

Using `RecordOnly` (instead of `Drop`) for rate-limited spans is important: it means the Activity is still created and data is still collected, so the `OnEnd` processor can check if the span *ended* as a failure and promote it to `RecordAndSample`.

### 4. Token Bucket per Operation

Each unique operation name (span `DisplayName`, e.g. `GET /api/orders/{id}`) gets its own `TokenBucket`:

```
TokenBucket {
    tokens: double         // current available tokens
    lastRefill: DateTime   // when tokens were last added
    rate: double           // tokens per second (= TracesPerSecond)
    capacity: double       // burst capacity (2× rate)
}
```

`TryConsume()`:
1. Calculate elapsed time since last refill
2. Add `elapsed × rate` tokens, capped at capacity
3. If `tokens >= 1`: decrement tokens, return **true** (sample)
4. Else: return **false** (drop / record-only)

The `capacity = 2 × rate` allows short bursts (e.g., cold start) to be fully sampled before the bucket empties.

### 5. Adaptive Rate Control

The adaptive controller runs on a background timer (every `SamplingPercentageDecreaseTimeout`, default 1 minute). It:

1. Counts total spans seen vs. sampled in the last interval
2. Calculates the **observed rate** (spans/second)
3. Calculates the **target sampling percentage**: `TracesPerSecond / observedRate`
4. Applies a moving average: `newRate = α × currentRate + (1-α) × targetRate`
5. Updates all per-operation token buckets with the new rate

This means:
- During a traffic spike: sampling % decreases quickly (controlled by `MovingAverageRatio`)
- After a spike subsides: sampling % increases back toward 100%
- The cost (exported spans) stays roughly constant at `TracesPerSecond` per operation

---

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

### TracesPerSecond

The target number of traces to export **per operation bucket** per second. 

- Default `1.0` means roughly 1 exported trace per second for each unique endpoint.
- For a service with 20 endpoints, this means ~20 exported traces/second total.
- Set higher (e.g., `5.0`) if you need more trace density for debugging.
- Set lower (e.g., `0.1`) to reduce costs on high-volume services.

### MovingAverageRatio (α)

Controls how quickly the sampler adapts to traffic changes.

- `0.0` = instant adaptation (jumpy, responsive)
- `0.5` = balanced (default)
- `0.9` = slow adaptation (stable, less responsive to spikes)

### SamplingPercentageDecreaseTimeout

How often to recalculate the adaptive rate. Default 1 minute.

- Decrease this (e.g., `00:00:30`) for faster adaptation to load changes.
- Increase this (e.g., `00:05:00`) for more stable sampling rates.

### EnablePerOperationBucketing

When `true` (default), each unique span name gets its own bucket. This ensures a chatty `GET /health` doesn't consume the budget meant for `POST /api/orders`.

When `false`, a single global bucket is used. This is simpler but less fair.

### MaxOperationBuckets

Maximum number of distinct operation buckets to maintain (default 100). When exceeded, additional operations share the global bucket. This prevents unbounded memory growth if operation names are dynamic (e.g., contain GUIDs).

If your application has more than 100 distinct operation types, increase this limit.

---

## Comparing v2.x vs v3.x Sampling

| Feature | AI SDK v2.x | Ark.Tools v3.x |
|---------|-------------|----------------|
| Adaptive rate | ✅ Yes (`SamplingPercentageEstimatorSettings`) | ✅ Yes (token bucket + adaptive controller) |
| Failure preservation | ✅ Yes (`DoNotSampleFailures` initializer) | ✅ Yes (sampler + failure promotion processor) |
| Per-operation buckets | ❌ No (global) | ✅ Yes |
| Pre-filtering | ✅ Yes (`ArkSkipUselessSpamTelemetryProcessor`) | ✅ Yes (`ArkPreFilterProcessor`) |
| API standard | Proprietary Application Insights | OpenTelemetry |

---

## Troubleshooting

### "I'm not seeing failures in Application Insights"

Check:
1. Is the `ArkAdaptiveSampler` registered? Check DI setup.
2. Is the error status being set on the Activity? (`activity.SetStatus(ActivityStatusCode.Error)`)
3. For HTTP failures, the instrumentation should auto-set status for 4xx/5xx.

### "Sampling rate is too high, costs are too large"

Reduce `TracesPerSecond`. For high-volume services, `0.1` or even `0.05` may be appropriate.

### "I'm missing data from a rarely-called endpoint"

Per-operation bucketing ensures rare endpoints do get sampled (their bucket never empties). If you're still not seeing them:
1. Check `MaxOperationBuckets` hasn't been exceeded
2. Enable verbose logging for the sampler (in development)

### "Sampling rate seems unstable"

Increase `MovingAverageRatio` (closer to 1.0) for more stable rates.

---

## Technical Notes

### Thread Safety

The `ArkAdaptiveSampler` and `TokenBucket` classes are fully thread-safe. Token consumption uses `Interlocked` operations on the token count, avoiding lock contention in high-throughput scenarios.

### Memory Usage

Each operation bucket uses approximately 100 bytes. With the default `MaxOperationBuckets=100`, the maximum overhead is ~10KB.

### Performance Overhead

Sampling decision time is typically sub-microsecond (token bucket check is O(1)). The adaptive rate controller runs on a background timer and does not impact the hot path.
