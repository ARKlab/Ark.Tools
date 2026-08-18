Now I have all the information needed to produce the research document. Here is the complete research outcome:

---

# Ark.Tools OpenTelemetry Implementation: Research Report

*Researched: 2026-08-18 — covers code and documentation in the `main` branch of `Ark.Tools/Ark.Tools`.*

---

## 1. Summary

Ark.Tools has been fully redesigned as **OpenTelemetry-first**. Instrumentation packages emit OpenTelemetry `ActivitySource`, `Meter`, and log records; Application Insights is reduced to an optional exporter, not a dependency of core instrumentation. The implementation is production-ready and has passed 54+ automated tests. Two open tasks remain: Rebus transport integration tests (require Docker) and secret-scanning / CodeQL passes.

---

## 2. Package Boundaries

The new package map separates exporter-neutral instrumentation from exporter-specific setup:

| Package | Responsibility | AI SDK dependency |
|---|---|---|
| `Ark.Tools.OTel` | Sampler, pre-filter, failure-promotion, SQL-filter, enrichment, resource helpers | **None** |
| `Ark.Tools.Rebus` | Rebus tracing (`OpenTelemetryStep`) and metrics (`OpenTelemetryProcessingMetricsStep`) | **None** |
| `Ark.Tools.ResourceWatcher` | `ResourceWatcherInstrumentation` constants, `ActivitySource`, `Meter` | **None** |
| `Ark.Tools.AspNetCore.OTel` | Azure Monitor Distro setup extension + `WebApi4xxAsSuccessProcessor` | **None** |
| `Ark.Tools.ResourceWatcher.OTel` | ResourceWatcher tracing/metrics registration for OTel pipeline | **None** |
| `Ark.Tools.ApplicationInsights` | AI v3 SDK customizations | Explicit |
| `Ark.Tools.AspNetCore.ApplicationInsights` | AI v3 ASP.NET Core hosting setup | Explicit |
| `Ark.Tools.ApplicationInsights.HostedService` | AI v3 worker hosting setup | Explicit |
| `Ark.Tools.ResourceWatcher.ApplicationInsights` | AI v3 ResourceWatcher adapter | Explicit |
| `Ark.Tools.Rebus.ApplicationInsights` | AI v3 Rebus compatibility adapter | Explicit |

**Key constraint verified:** `Ark.Tools.AspNetCore`, `Ark.Tools.AspNetCore.MinimalApi`, and `Ark.Tools.ResourceWatcher.WorkerHost.Hosting` do **not** transitively install Application Insights. Telemetry setup is always an explicit application-level call.

---

## 3. Core Implementation: `Ark.Tools.OTel`

**File:** `src/common/Ark.Tools.OTel/` (all files confirmed present and reviewed)

### 3.1 `ArkAdaptiveSampler` — `ArkAdaptiveSampler.cs`

A parent-sampling enhancement using adaptive per-operation token buckets.

**Decision logic** (`ShouldSample`, lines 76–118):

1. Tag `ark.filtered = true` (set by `ArkPreFilterProcessor`) → **Drop**
2. `ActivityTraceFlags.Recorded` on parent → **RecordAndSample** (chain continuity)
3. Parent with any `TraceId` but *not* recorded → **RecordOnly** (no child contradiction)
4. Trace ID in `FailedTraceRegistry` → **RecordAndSample** (sibling promotion)
5. Per-operation token bucket: `TryConsume()` succeeds → **RecordAndSample**; fails → **RecordOnly** (never Drop — allows failure promotion at `OnEnd`)

**Adaptive controller** (`_runAdaptiveControllerAsync`/`_adjustRate`, lines 136–187): a background `Task` fires every `SamplingPercentageDecreaseTimeout` (default 1 min). It reads/resets counters, computes a target sampling ratio, applies a moving-average (`MovingAverageRatio`, default 0.5), and pushes the updated rate to all `OperationBucket` instances.

**Overflow protection:** when `_buckets.Count >= MaxOperationBuckets` a single `__overflow__` bucket absorbs all new operations. Default cap: 100 buckets. Global bucketing uses `__global__`.

### 3.2 `OperationBucket` — `OperationBucket.cs`

Thread-safe token bucket (lock-based). Initial pre-fill: `rate × 2.0` tokens (2-second burst). Cap also `rate × 2.0`. `UpdateRate(double)` is called by the adaptive controller.

### 3.3 `FailedTraceRegistry` — `FailedTraceRegistry.cs`

Shared between sampler and failure-promotion processor. Stores `(ActivityTraceId → tickCount64)` in a `ConcurrentDictionary`. TTL: default 5 minutes; cleanup runs at most once per minute. Thread-safe via double-checked locking.

### 3.4 `ArkPreFilterProcessor` — `ArkPreFilterProcessor.cs`

`OnStart` processor. Drops high-volume, low-value spans by clearing `ActivityTraceFlags.Recorded` and `IsAllDataRequested`. Rules (all success-only):

- HTTP `OPTIONS` requests — checked via `http.request.method` tag or `DisplayName` prefix
- Azure Service Bus `Receive` — checked via `messaging.system=servicebus` + `messaging.operation=receive`, or `ServiceBusReceiver.*` display-name prefix
- SQL `Commit` — checked via `db.operation` / `db.operation.name = Commit`

Only successful spans are suppressed; if the span turns out to be a failure, `ArkFailurePromotionProcessor` cannot un-suppress it (by design — pre-filtered spans are structurally dropped at `OnStart`).

### 3.5 `ArkFailurePromotionProcessor` — `ArkFailurePromotionProcessor.cs`

`OnEnd` processor. Inspects every completed span and promotes rate-limited (`RecordOnly`) spans to `RecordAndSample` when a failure is detected.

**Failure detection criteria** (`_isFailure`, lines 114–148):
- `ActivityStatusCode.Error`
- `exception` event on the span
- `http.response.status_code >= 400` (int or string tag)
- `rpc.grpc.status_code != 0` (int or string tag)

**Promotion logic:**
- Failing span itself is promoted
- Entire local parent chain is walked (`Activity.Parent`) and promoted
- `FailedTraceRegistry.Register(traceId)` is called so siblings completing *after* the failure are also promoted
- Already-sampled spans only register the failure; they are not re-processed

**Known limitation:** siblings that completed *before* the failure was detected cannot be recovered.

### 3.6 `WebApi4xxAsSuccessProcessor` — `src/aspnetcore/Ark.Tools.AspNetCore.OTel/WebApi4xxAsSuccessProcessor.cs`

`OnEnd` processor for `ActivityKind.Server` spans. Reads `http.response.status_code`; if `>= 400 and < 500` sets `ActivityStatusCode.Unset`. Must run **before** `ArkFailurePromotionProcessor` in the pipeline to prevent 4xx responses from triggering failure promotion.

### 3.7 `ArkSqlDependencyFilterProcessor` — `ArkSqlDependencyFilterProcessor.cs`

`OnStart` processor. Filters SQL spans targeting a specific `Data Source` + `Initial Catalog` (intended for NLog audit databases). Parses `SqlConnectionStringBuilder`; disabled if the connection string is null/empty or fails to parse. Matches via `server.address`/`net.peer.name`/`peer.service` and `db.name`/`db.namespace`. Drops by clearing `Recorded` and `IsAllDataRequested`.

### 3.8 `ArkTelemetryEnrichmentProcessor` — `ArkTelemetryEnrichmentProcessor.cs`

`OnStart` processor. Adds `ProcessName` tag (entry assembly name) to every span. This is a legacy span-level tag. The preferred approach for new applications is `ResourceBuilder.AddArkTelemetryResource()` which sets `service.name` on the OTel Resource.

### 3.9 `ArkTelemetryResourceExtensions` — `ArkTelemetryResourceExtensions.cs`

Extension method: `ResourceBuilder.AddArkTelemetryResource()`. Sets `service.name` to the entry assembly name. Used in `AddArkAspNetCoreOpenTelemetry` and `AddArkResourceWatcherOpenTelemetry`.

---

## 4. ASP.NET Core OTel Setup: `Ark.Tools.AspNetCore.OTel`

**File:** `src/aspnetcore/Ark.Tools.AspNetCore.OTel/Ex.cs`

Two public extension methods:

### `AddArkAspNetCoreOpenTelemetry(OpenTelemetryBuilder)`

Exporter-agnostic. Registers:
- `ResourceBuilder.AddArkTelemetryResource()` (sets `service.name`)
- `AddSource("ark.tools.rebus")` — Rebus activity source
- `HttpClient` instrumentation (`OpenTelemetry.Instrumentation.Http`)
- SQL Client instrumentation (`OpenTelemetry.Instrumentation.SqlClient`)
- `AddSource("Azure.Messaging.ServiceBus")` — Azure SDK experimental source
- `WebApi4xxAsSuccessProcessor` (4xx normalization)
- Rebus metrics meter: `OpenTelemetryProcessingMetricsStep.MeterName`

### `AddArkAzureMonitorOpenTelemetry(IServiceCollection, IConfiguration?)`

Recommended one-call setup for new ASP.NET Core apps:

```csharp
builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);
```

Reads `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING` (config first, then env var). Calls `UseAzureMonitor(...)` if a connection string is found. Configures the OTel logger provider filter to `LogLevel.Error` and above (NLog handles lower-severity targets independently). Delegates instrumentation to `AddArkAspNetCoreOpenTelemetry`.

> **Note:** Azure Service Bus tracing requires `AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true)` *before* constructing clients and before calling the setup extension.

**NuGet dependencies** (from `.csproj`):
- `Azure.Monitor.OpenTelemetry.AspNetCore`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.SqlClient`
- Framework reference: `Microsoft.AspNetCore.App`
- Project references: `Ark.Tools.Rebus`, `Ark.Tools.OTel`

---

## 5. Rebus Instrumentation: `Ark.Tools.Rebus`

### `OpenTelemetryStep` — `OpenTelemetryStep.cs`

Activity source: `ark.tools.rebus`. Incoming span name: `ark.tools.rebus.process | <messageType>`, kind `Consumer`.

Tags set on incoming spans:
- `messaging.system = "rebus"`
- `messaging.operation.type = "process"`
- `messaging.message.id`
- `messaging.message.type`
- `rebus.correlation_id`

Exceptions: recorded as `exception` event with `exception.type`, `exception.message`, `exception.stacktrace` tags, plus `ActivityStatusCode.Error`.

Outgoing step: propagates `Activity.Current.Id` into `Diagnostic-Id` header (W3C trace context).

Context extraction: parses `Diagnostic-Id` header as `ActivityContext` (W3C) with optional `TraceStateString`.

### `OpenTelemetryProcessingMetricsStep` — `OpenTelemetryProcessingMetricsStep.cs`

Meter: `ark.tools.rebus`. Instruments:

| OTel Histogram | Unit | Attributes | Semantics |
|---|---|---|---|
| `ark.tools.rebus.message_time_in_queue_success` | ms | `message.type` | Emitted only on success; `SentTime` header parse errors are silently swallowed |
| `ark.tools.rebus.message_processing_time` | ms | `message.type`, `operation.result` | Always emitted; `operation.result` = `"success"` or `"failure"` |

Queue time: `_time.Now − enqueuedTime − stopwatch.Elapsed`. Values clamped to `>= 0`.

### Registration extensions — `Ex.cs`

- `UseOpenTelemetry(OptionsConfigurer, Container)` — injects `OpenTelemetryStep` at the front of receive pipeline and before `SerializeOutgoingMessageStep` on send.
- `UseOpenTelemetryMetrics(OptionsConfigurer, Container)` — injects `OpenTelemetryProcessingMetricsStep` before `DispatchIncomingMessageStep`.

---

## 6. ResourceWatcher OTel: `Ark.Tools.ResourceWatcher` + `Ark.Tools.ResourceWatcher.OTel`

### `ResourceWatcherInstrumentation` — `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcherInstrumentation.cs`

Public constants (the OTel instrumentation contract):

| Constant | Value |
|---|---|
| `DiagnosticListenerName` | `"Ark.Tools.ResourceWatcher"` |
| `ActivitySourceName` | `"ark.tools.resourcewatcher"` |
| `ActivityNamePrefix` | `"ark.tools.resourcewatcher"` |
| `MeterName` | `"ark.tools.resourcewatcher"` |
| `ExceptionEventName` | `"ark.tools.resourcewatcher.exception"` |

All names follow lowercase dot-separated OTel naming conventions.

### `AddArkResourceWatcherOpenTelemetry(OpenTelemetryBuilder)` — `src/resourcewatcher/Ark.Tools.ResourceWatcher.OTel/Ex.cs`

Registers:
- `ResourceBuilder.AddArkTelemetryResource()`
- `AddSource(ResourceWatcherInstrumentation.ActivitySourceName)`
- `AddMeter(ResourceWatcherInstrumentation.MeterName)`

### `AddArkOpenTelemetryForWorkerHost(IHostBuilder)` — same file

Convenience wrapper; calls `services.AddOpenTelemetry().AddArkResourceWatcherOpenTelemetry()` during `ConfigureServices`.

---

## 7. Package Versions in Use

From `docs/otel/applicationinsights-migration/nuget-research.md` (validated 2026-08-15):

| Package | Version |
|---|---|
| `Microsoft.ApplicationInsights` | 3.1.2 |
| `Microsoft.ApplicationInsights.AspNetCore` | 3.1.2 |
| `Microsoft.ApplicationInsights.WorkerService` | 3.1.2 |
| `OpenTelemetry` | 1.17.0 |
| `OpenTelemetry.Api` | 1.17.0 |
| `OpenTelemetry.Extensions.Hosting` | 1.17.0 |
| `OpenTelemetry.Extensions.Propagators` | 1.17.0 |
| `Azure.Monitor.OpenTelemetry.Profiler` | 1.0.1-beta.7 (prerelease) |

The profiler is centrally versioned and explicitly flagged as removable if deployment validation fails.

---

## 8. Processor Ordering

The documented pipeline order for a Web API host:

```
[OpenTelemetry SDK - ActivitySource]
        │ Activity started
        ▼
[ArkPreFilterProcessor.OnStart]       ← drop noise before sampling
        │
        ▼
[ArkAdaptiveSampler.ShouldSample]     ← root token bucket or parent propagation
        │
        ▼
[ResourceBuilder.AddArkTelemetryResource]  ← service.name on resource
        │
        ▼
[... activity executes ...]
        │
        ▼
[WebApi4xxAsSuccessProcessor.OnEnd]   ← clear error status for HTTP 4xx (MUST run before failure promotion)
        │
        ▼
[ArkFailurePromotionProcessor.OnEnd]  ← promote failed spans + parent chain
        │
        ▼
[Azure Monitor Exporter → Application Insights]
```

---

## 9. Telemetry Breaking Changes (Migration Surface)

From `docs/otel/upgrade-guide.md`:

| Area | Before (AI v2/classic) | After (OTel) | Monitoring adaptation |
|---|---|---|---|
| Rebus processing | AI `RequestTelemetry` | OTel consumer span `ark.tools.rebus.process \| <type>` | Query spans and `messaging.*`/`rebus.*` attributes |
| Rebus queue time | AI metric `Rebus / MessageTimeInQueueSuccess` | OTel histogram `ark.tools.rebus.message_time_in_queue_success` | Change query syntax; keep `message.type` |
| Rebus processing time | AI metric `Rebus / MessageProcessingTime` | OTel histogram `ark.tools.rebus.message_processing_time` | Change query syntax; keep dims |
| Rebus result | `OperationResult=success/failure` | `operation.result=success/failure` | Rename attribute only |
| ResourceWatcher run/process | AI request telemetry | OTel internal spans, lowercase dot names | Remove telemetry-type filter |
| ResourceWatcher fetch/state | AI dependency telemetry `Type=ProcessStep` | OTel internal spans | Replace dependency-type filter with span-name filter |
| ResourceWatcher exceptions | AI `ExceptionTelemetry` | OTel exception event + error status | Query exception events/status |
| HTTP 4xx | AI processor cleared failure | OTel processor clears server span error status | Keep 4xx-as-success at span-status level |
| Registration | Hosting package registered AI implicitly | No automatic registration | Add one explicit setup call |

---

## 10. Sampling Configuration Reference

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

| Option | Default | Description |
|---|---|---|
| `TracesPerSecond` | `1.0` | Target traces/s per operation bucket |
| `MovingAverageRatio` | `0.5` | Smoothing factor (0 = instant, 1 = no adaptation) |
| `SamplingPercentageDecreaseTimeout` | `00:01:00` | Adaptive controller evaluation interval |
| `EnablePerOperationBucketing` | `true` | Per-operation fairness; disabling collapses all to `__global__` |
| `MaxOperationBuckets` | `100` | Memory cap; overflow uses `__overflow__` bucket |

---

## 11. Test Coverage

| Test project | Tests | Status |
|---|---|---|
| `Ark.Tools.OTel.Tests` | Sampling, enrichment, ResourceWatcher OTel | 54 passed (.NET 8 + .NET 10) |
| `Ark.Tools.Rebus.Tests` | `OpenTelemetryStepTests`, metrics step tests | 4 passed (.NET 8 + .NET 10) |

Test infrastructure uses `ActivityListener` + `CollectingProcessor` harness to verify `Recorded` flag decisions without an exporter.

---

## 12. Relevant Source Files

| File | Purpose |
|---|---|
| `src/common/Ark.Tools.OTel/ArkAdaptiveSampler.cs` | Sampler with per-op token buckets and adaptive rate |
| `src/common/Ark.Tools.OTel/ArkAdaptiveSamplerOptions.cs` | Sampler configuration POCO |
| `src/common/Ark.Tools.OTel/OperationBucket.cs` | Token bucket (2× burst, lock-based) |
| `src/common/Ark.Tools.OTel/FailedTraceRegistry.cs` | Failed-trace registry, 5 min TTL |
| `src/common/Ark.Tools.OTel/ArkPreFilterProcessor.cs` | Pre-filter (OPTIONS, SB Receive, SQL Commit) |
| `src/common/Ark.Tools.OTel/ArkFailurePromotionProcessor.cs` | Failure promotion with parent-chain walk |
| `src/common/Ark.Tools.OTel/ArkSqlDependencyFilterProcessor.cs` | Optional NLog DB SQL filter |
| `src/common/Ark.Tools.OTel/ArkTelemetryEnrichmentProcessor.cs` | Per-span `ProcessName` tag enricher |
| `src/common/Ark.Tools.OTel/ArkTelemetryResourceExtensions.cs` | `service.name` resource builder extension |
| `src/aspnetcore/Ark.Tools.AspNetCore.OTel/Ex.cs` | `AddArkAzureMonitorOpenTelemetry` + `AddArkAspNetCoreOpenTelemetry` |
| `src/aspnetcore/Ark.Tools.AspNetCore.OTel/WebApi4xxAsSuccessProcessor.cs` | 4xx-as-success processor |
| `src/common/Ark.Tools.Rebus/OpenTelemetryStep.cs` | Rebus consumer/producer activity source |
| `src/common/Ark.Tools.Rebus/OpenTelemetryProcessingMetricsStep.cs` | Rebus queue/processing metrics |
| `src/common/Ark.Tools.Rebus/Ex.cs` | `UseOpenTelemetry` + `UseOpenTelemetryMetrics` |
| `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcherInstrumentation.cs` | OTel instrumentation contract constants |
| `src/resourcewatcher/Ark.Tools.ResourceWatcher.OTel/Ex.cs` | `AddArkResourceWatcherOpenTelemetry` + `AddArkOpenTelemetryForWorkerHost` |
| `tests/Ark.Tools.OTel.Tests/OTelSamplingTests.cs` | Sampling pipeline tests |
| `tests/Ark.Tools.Rebus.Tests/OpenTelemetryStepTests.cs` | Rebus step tests |

---

## 13. Gaps and Open Items

### Open implementation tasks (from `docs/otel/progress/telemetry-refresh.md`)

1. **Transport integration tests** — Rebus transport-level tests require Docker and have not yet run. Logic tests pass.
2. **Secret scanning** — not yet run on modified files (handoff task).
3. **Code review and CodeQL** — not yet completed (handoff task).
4. **Full solution restore** — blocked by pre-existing audit findings in unrelated dependencies. Targeted restore/build passes; full clean lockfile requires upstream fixes.

### Design gaps observed in code

1. **`ArkAdaptiveSampler` background task lifecycle** — the `_runAdaptiveControllerAsync` `Task` is fire-and-forget (`_ = Task.Run(...)`). There is no `CancellationToken` threading it to the `TracerProvider` lifetime, so it will continue running until the process exits. This is low-risk but non-ideal for test isolation and graceful shutdown.

2. **`ArkPreFilterProcessor` success filter limitation** — pre-filtering happens at `OnStart`, before response status is known. `OPTIONS` and Service Bus `Receive` spans are always dropped, even on failure. The processor comment acknowledges this for HTTP (`"rely on the failure promotion processor to catch it if it fails later"`) but failure promotion cannot act on a span with `IsAllDataRequested = false` because it has already been structurally excluded. This means failed `OPTIONS` responses and failed Service Bus `Receive` operations are silently discarded.

3. **`ArkTelemetryEnrichmentProcessor` vs. resource** — the processor adds `ProcessName` as a per-span tag. The modern OTel approach (`AddArkTelemetryResource` / `service.name`) is correct for new pipelines, but the enrichment processor is still present and may produce duplicate/redundant data if both are registered together. No documentation notes when to prefer one over the other.

4. **`ArkAdaptiveSampler` — no DI registration pattern documented** — the shared `FailedTraceRegistry` must be passed to both the sampler and `ArkFailurePromotionProcessor` for full failure-promotion coordination. The documentation and `AddArkAspNetCoreOpenTelemetry` extension do not show how to wire this in practice; each class defaults to its own private registry if constructed separately.

5. **Further instrumentation candidates not wired** — gRPC client, EF Core, Redis, MongoDB/Npgsql are documented as candidates in `README.md` but are deliberately opt-in and not registered by any current setup extension.

6. **`Azure.Monitor.OpenTelemetry.Profiler` is prerelease** — the `1.0.1-beta.7` profiler is enabled by default in the setup extension (as listed in `csproj` NuGet lock) but is flagged as requiring per-platform deployment validation before relying on it.

---

## 14. Recommendations

1. **Tie the adaptive sampler background task to a `CancellationToken`** linked to `IHostApplicationLifetime.ApplicationStopping` to ensure clean shutdown and test isolation.

2. **Re-evaluate pre-filtering at `OnEnd`** for cases where a filtered operation type (e.g., Service Bus `Receive`) can fail. Consider moving the filter to `OnEnd` once the response status is known, or limit pre-filtering to span types that structurally cannot fail (e.g., SQL `Commit`).

3. **Document and test the shared `FailedTraceRegistry` wiring pattern**. Add a DI registration helper or builder extension that creates one registry, passes it to both `ArkAdaptiveSampler` and `ArkFailurePromotionProcessor`, and registers both with the `TracerProviderBuilder`.

4. **Clarify `ArkTelemetryEnrichmentProcessor` vs. `AddArkTelemetryResource`**. Document which to use and whether both can be active simultaneously. If `AddArkTelemetryResource` covers all cases, mark `ArkTelemetryEnrichmentProcessor` as obsolete.

5. **Resolve full solution restore** by addressing the pre-existing NuGet audit findings in unrelated packages. A clean lockfile baseline is important before the next release.

6. **Complete handoff tasks** (secret scanning, code review, CodeQL) before merging the OTel branch to `main`.

7. **Validate the Azure Monitor Profiler** on a real deployment target before treating it as a stable default; document the opt-out path clearly.