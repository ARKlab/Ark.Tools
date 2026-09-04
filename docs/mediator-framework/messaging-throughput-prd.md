# PRD — High-throughput messaging receivers for the Mediator Framework

Status: **proposed**; research complete, awaiting review. Implementation is
tracked as the [`AMF` series](progress/tasks/README.md#messaging-throughput-amf)
on the task board.

Owner: Ark.Tools Mediator Framework (messaging). Scope: the split between the
*sending* and *processing* seams of `IMessagingTransport`, the host-side
processing runtime, transport profiles (Azure Service Bus, Azure Storage Queues,
in-memory, Azure Functions), resource provisioning, and messaging metrics.
Explicitly out of scope: `Ark.Tools.Rebus`, which remains a separate alternative
stack (§3).

## 1. Problem

A MediatorFramework receiver host processes **one message at a time**, forever,
regardless of how many cores it owns.

`MessagingReceivePump._runAsync` is the whole concurrency model:

```csharp
await foreach (var delivery in _transport.ReceiveAsync(_queue, ctk))
    await _onDelivery(delivery, ctk);
```

`MessagingReceivePump.cs:101-105`. Everything downstream — `MessagingDispatcher.OnDeliveryAsync`,
the SimpleInjector scope, the incoming pipeline, settlement — is serialized behind
that single `await`. Sustained throughput is therefore

> `throughput ≈ 1 / (receive RTT + handler duration + settle RTT)`

which for Azure Service Bus is roughly **20–60 msg/s on any host size**, with the
CPU idle. Adding cores, or vCPU-based scale-out, buys nothing.

The transport seam is the reason the runtime cannot do better:

| Symptom | Root cause in current code |
| --- | --- |
| No parallelism | `IAsyncEnumerable<IMessagingLockedDelivery>` gives the host a stream with no credit accounting, no batch boundary, and no way to express "receive more while I process these". |
| No way to detect an empty queue | The enumerable simply does not yield. The host cannot distinguish "empty" from "slow broker", so it cannot back off. Storage Queues works around this *inside* the transport with a hard-coded `Task.Delay(250 ms)` (`StorageQueueMessagingTransport.cs:254`), i.e. a fixed billed-transaction floor of 4 req/s per queue, per host, forever. |
| One broker round trip per message | `ServiceBusMessagingTransport._receiveAsync` calls `ReceiveMessageAsync(1 s)` per message (`ServiceBusMessagingTransport.cs:169`); Storage Queues calls `ReceiveMessagesAsync(maxMessages: 1)` (`StorageQueueMessagingTransport.cs:248`) although the service allows 32. |
| No prefetch | Nothing in `src/mediator-framework` sets `PrefetchCount`, `MaxConcurrentCalls`, or any concurrency knob — the grep returns zero hits. |
| Lock renewal is not lock-aware | `MessagingDispatcher._renewLockAsync` renews on a fixed 15 s `Task.Delay` loop (`MessagingDispatcher.cs:341-350`), one dedicated loop per in-flight message, ignoring the entity's actual `LockDuration` and ignoring time already spent in any client-side buffer. It cannot survive prefetching. |
| No backpressure | There is no local buffer, so there is nothing to be full; conversely there is no signal that could ever slow the receive side down when the handler's downstream (SQL pool, HTTP dependency) saturates. |
| Provisioned entities are not throughput-shaped | `ServiceBusTransportManagement.EnsureQueueAsync` sets only `MaxDeliveryCount` and `UserMetadata` (`ServiceBusTransportManagement.cs:34-38`). `EnablePartitioning` is **immutable after creation**, so a queue created today can never become a high-throughput queue later. |
| No throughput observability | `MessagingMetrics` records per-message duration/outcome/attempts only. There is no in-flight gauge, no concurrency-limit gauge, no empty-receive counter, no lock-renewal failure counter — nothing that can drive tuning, alerting, or KEDA. |

Secondary problem: the seam conflates **sending** and **processing**, which are
two different concerns with two different audiences.

- **Sending** is universal. Every host — a Minimal API service, a gRPC service, an
  Azure Function, a worker — sends and publishes through `IBus` /
  `IMessagingTransport`. It needs destinations, sizing, scheduling and the
  DataBus claim-check. Nothing about it is throughput-critical in the way
  processing is, and nothing in this PRD changes it.
- **Processing** is host-specific and is where the throughput problem lives. A
  processor host needs credits, batches, lock lifetimes, a concurrency limit, and
  a way to say "stop giving me work".

Those two live behind the same interface today, so any change to the processing
model risks the send path, and the send path's transport-neutrality prevents the
processing model from using transport-native features. Separating them is what
makes the rest of this PRD possible — and it is what keeps Azure Functions
receivers unaffected: they keep sending through the unchanged `IBus`, and their
triggering stays the Functions host's job.

## 2. Goals

1. **Sending and processing are separate concerns.** The send path (`IBus`,
   `IMessagingTransport`) is unchanged and stays usable identically from every
   host — Minimal API, gRPC, worker, and Azure Functions. Only the processing
   runtime changes.
2. A processor host **saturates its real bottleneck**, not the framework's
   control flow. For a compute-bound handler that bottleneck is the CPU; for an
   I/O-bound handler it is the *useful concurrency* of the remote dependency, and
   the host must converge on it instead of growing without limit (§6.2).
3. Concurrency is **adaptive and bounded**. The host raises parallelism only while
   added workers demonstrably raise throughput *and* do not inflate handler
   latency, and lowers it on broker throttling, lock loss, handler timeouts,
   thread-pool starvation, latency inflation, or an explicit downstream
   backpressure signal.
4. Prefetch and in-flight buffering are **credit-bounded and lock-safe**: the host
   never holds more locked deliveries than it can process well inside the lock
   duration, and lock renewal covers the *entire* lock lifetime, buffer time
   included.
5. An empty queue costs **asymptotically nothing**: a receive can say "no
   messages", and the host backs off exponentially with jitter up to a bounded
   maximum, resetting on the first non-empty result.
6. Transports can expose their **native strengths** — batch receive, prefetch
   credits, native pumps, partitioned entities, long-poll windows — without those
   concepts leaking into handlers or into transports that lack them.
7. Where a provider already ships a correct, maintained pump (`ServiceBusProcessor`),
   the framework **uses it** rather than reimplementing credit management and lock
   renewal.
8. Hosts that own triggering themselves (Azure Functions) are **unchanged** and are
   prevented from composing a second pump.
9. Throughput is observable through a **small set of always-on operational
   instruments** sufficient for reliability alerting and autoscaling, plus an
   **opt-in advanced set** for tuning and debugging (§10).
10. Settlement semantics, retry policy, second-level retries, fail-fast
    dead-lettering, and per-message DI scoping are unchanged.

## 3. Non-goals

- **Anything about Rebus.** `Ark.Tools.Rebus` and the Rebus-hosted processors
  remain a separate, fully supported alternative to MediatorFramework Messaging
  until Messaging is mature. This PRD does not wrap, adapt, replace or tune
  Rebus; Rebus appears here only as researched prior art (§4.2). Messaging's own
  advantage — the same participant hosted either as an Azure Functions FaaS
  receiver or as a long-running service (for example a Kubernetes container),
  which Rebus cannot do — is exactly what makes a first-class processor host
  worth building.
- Changing the Azure Functions receiver model. Functions receivers keep their
  generated triggers, the Functions host's concurrency settings, and the
  unchanged `MessagingDispatcher`.
- Ordered processing and message sessions. (Noted where partitioning interacts
  with them; not implemented.)
- Exactly-once delivery. The framework stays at-least-once.
- Autoscaling infrastructure. The PRD emits the metrics KEDA/Container Apps need;
  it does not provision scalers.
- Batch *handler* invocation (`IHandler<IReadOnlyList<T>>`). Batch **receive** is
  in scope; batch **dispatch** is not.
- Batch settlement. Neither `Azure.Messaging.ServiceBus` nor
  `Azure.Storage.Queues` exposes a batch complete/delete for individually locked
  messages; there is nothing to call.
- Rewriting the send path. Send-side batching is a separate, smaller
  optimisation (see [§16 Delivery](#16-delivery), `AMF-09`).
- Replacing `MessagingDispatcher`. Its per-delivery semantics stay; only its lock
  renewal moves out.

## 4. Research summary

### 4.1 Current Ark implementation

| Element | File | Behaviour |
| --- | --- | --- |
| Send seam | `IMessagingTransport.cs:9-66` | `SendAsync`, `PublishAsync`, native sizing/measuring. Adequate; unchanged by this PRD. |
| Receive seam | `IMessagingTransport.cs:88-95` | `IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(queue, ctk)`. No batch, no credits, no empty signal, no capability reporting. |
| Delivery | `IMessagingTransport.cs:98-130` | Headers, payload, `DeliveryCount`, `RenewLockAsync`, `CompleteAsync`, `AbandonAsync`, `DeadLetterAsync`. Shape is good; missing lock expiry instant and native message id. |
| Pump | `MessagingReceivePump.cs` | Strictly sequential `await foreach`. |
| Dispatch | `MessagingDispatcher.cs:92-175` | Per-delivery: classify headers, prepare payload, DI scope, pipeline, settle, metrics. Handler stage is bounded by `retryPolicy.MaximumHandlerDuration`; a per-message renewal loop runs alongside. |
| Settlement rules | `MessagingSettlement.cs:41-60` | Complete / Abandon / DeadLetter / RunSecondLevel from delivery count + classification. Unchanged. |
| Host wiring | `FluentMessagingComposition.cs:709-754` | `MessagingReceiveHostedService` news up one pump per participant queue. |
| Functions wiring | `MessagingFunctionsServiceCollectionExtensions.cs`, `MessagingQueueFunctionsDispatcher.cs` | Trigger-driven; concurrency is the Functions host's. No framework pump — correct today by accident, must become explicit. |
| Provisioning | `ServiceBusTransportManagement.cs:24-52` | `MaxDeliveryCount` + owner metadata only; reconcile updates delivery count. |
| Metrics | `OpenTelemetryProcessingMetricsStep.cs:69+` | `messaging.client.operation.duration`, `messaging.process.duration`, `messaging.message.time_in_queue`, `messaging.process.messages`, `messaging.process.attempts`. |
| Retry policy | `IMessagingRetryPolicy.cs` | `MaximumDeliveryCount`, `SecondLevelRetriesEnabled`, `MaximumHandlerDuration`, `RetryDelay`. `MaximumHandlerDuration` is the natural input for lock and prefetch budgeting. |

### 4.2 Rebus

Rebus is the closest prior art and the explicit reference in the problem
statement. Its model, from source:

- `ITransport.Receive(ITransactionContext, CancellationToken)` returns **one
  `TransportMessage` or `null`** — no batch API anywhere in the abstraction. `null`
  *is* the empty signal, which is the one thing Ark's enumerable lacks.
- Settlement is per message and per transaction context, registered as
  `OnAck`/`OnNack` callbacks inside `Receive`.
- `ThreadPoolWorkerFactory` builds **one shared `ParallelOperationsManager`** —
  a `SemaphoreSlim(MaxParallelism)` — for all workers. Each `IWorker` is a real
  background `Thread` whose loop does `TryBegin()` (non-blocking
  `Wait(TimeSpan.Zero)`); on success it **fire-and-forgets** the receive+process
  continuation and immediately loops. So "worker" is a dispatcher thread and
  `MaxParallelism` is the real concurrency limit.
- Defaults: `NumberOfWorkers = 1`, `MaxParallelism = 5`,
  `WorkerShutdownTimeout = 1 min`, `TransportReceiveErrorCooldownTime = 30 s`.
  `SetNumberOfWorkers` is runtime-settable and clamped to `MaxParallelism`;
  `MaxParallelism` is fixed once the semaphore is constructed.
- `IBackoffStrategy` separates *no message* (`WaitNoMessageAsync`), *no free slot*
  (`WaitAsync`) and *error* (`WaitErrorAsync`, fixed 30 s) waits. The single
  `DefaultBackoffStrategy` indexes a `TimeSpan[]` by **whole seconds spent idle**,
  saturating on the last entry; default table is `100 ms × 10` then `250 ms`
  forever. `Reset()` is global and fires whenever any worker receives a message.
- `Rebus.AzureServiceBus` keeps one `ServiceBusReceiver` with
  `PrefetchCount = _prefetchCount`, PeekLock, and calls **`ReceiveMessageAsync`
  (singular)** — the batch `ReceiveMessagesAsync` is used only for purging.
  Lock renewal is a hand-rolled `MessageLockRenewer` (renew at 50 % of remaining
  lock) driven by a **coarse 10 s polling task**, and it is **disabled whenever
  prefetching is enabled** (`EnablePrefetching`: "the automatic peek lock renewal
  will be disabled"). `EnablePartitioning()` exists and warns that it cannot be
  changed after creation.

What to take: the `null`/empty signal, the three-way backoff split (no message /
no capacity / error), the shared concurrency limiter, 50 %-of-remaining lock
renewal, and the partitioning-is-immutable warning.

What to reject: thread-per-worker; a semaphore slot held *during* idle backoff
(the commented-out `parallelOperation.Dispose()` is deliberate, and it means idle
workers consume parallelism); static `MaxParallelism`; one broker round trip per
message; per-second backoff granularity with a globally shared reset; and above
all **prefetch XOR lock renewal**, which is a false dichotomy — the correct fix is
to bound the prefetch buffer by measured drain time, not to switch renewal off.

### 4.3 `ServiceBusProcessor` and MassTransit

`Azure.Messaging.ServiceBus.ServiceBusProcessor` is a push pump that already
solves the hard parts: `MaxConcurrentCalls` (default 1), `PrefetchCount`
(default 0, credit-based local cache), `MaxAutoLockRenewalDuration` (default
5 min, `Timeout.InfiniteTimeSpan` supported), `AutoCompleteMessages` (default
true — must be **false** for us, we settle explicitly), plus a supported error
handler and graceful stop.

MassTransit does not reimplement it: `ServiceBusConnectionContext` fills
`ServiceBusProcessorOptions` from endpoint settings, and unifies the knob under a
transport-neutral `ConcurrentMessageLimit` (`MaxConcurrentCalls` is `[Obsolete]`
in its configurator). It also warns/adjusts when `PrefetchCount` is smaller than
the per-session message limit.

This is the model to copy for Service Bus: one transport-neutral concurrency
concept, mapped onto the provider's native pump.

### 4.4 Provider facts that constrain the design

| Fact | Consequence |
| --- | --- |
| ASB partitioning must be chosen **at entity creation** and cannot be changed (Standard: per-entity `EnablePartitioning`; Premium: a namespace-level 1/2/4/8/16 choice made at namespace creation). | Partitioning becomes an explicit option on the **host's transport declaration** (`UseServiceBus(client, o => o.EnablePartitioning = true)`) or on runtime setup (`MessagingProcessingOptions` / configuration binding), applied at create time only; the reconciler must **detect and report** a mismatch on an existing entity rather than silently continue (§8, §9). |
| Transactions and send-batches cannot span partitions; `SessionId` is the partition key. | Partitioning is opt-in per network/participant, and mutually exclusive with cross-entity transactional sends. |
| Prefetched ASB messages are **already locked** and their lock clock runs while buffered. | Prefetch budget must derive from measured processing rate × concurrency vs. lock duration, and renewal must start at fetch time. |
| ASB queue `LockDuration` default 60 s, max 5 min; `MaxDeliveryCount` default 10. | Renewal cadence must come from the entity, not a constant. `MaximumHandlerDuration` must be reconciled against `LockDuration × renewal capability`. |
| Storage Queues: `ReceiveMessages` returns up to **32** messages per call; every call is a billed transaction; visibility timeout up to 7 days; no server-side long poll. | Batch receive is the single biggest win; adaptive idle backoff is a **cost** control, not only a latency control. |
| Storage Queues has no lock renewal, only `UpdateMessage` with a new visibility timeout, which rotates the pop receipt. | Renewal must own the pop receipt, and concurrent settle must use the latest one — the existing `StorageQueueLockedDelivery` already rotates `_popReceipt`, but it is not thread-safe against a concurrent renewal + settle. |
| Azure Functions owns concurrency in `host.json` (`maxConcurrentCalls`, `prefetchCount`, dynamic concurrency) and settles via the binding. | Functions receivers are **out of scope for the processing runtime**: no framework pump, no concurrency controller, no change to the generated triggers or the dispatcher. Sending/publishing through `IBus` is identical to every other host. Composing a processor host inside a Function app fails startup. |

## 5. Solution shape

The first split is **sending vs. processing**. `IMessagingTransport` — send,
publish, schedule, sizing, DataBus — is untouched, and remains the only messaging
seam an Azure Function (or any producer-only host) ever sees. Everything new in
this PRD sits on the *processing* side and is composed only by processor hosts.

The processing side is then split into two seams by who owns the pump, plus one
host runtime.

```
   every host, unchanged
   (Minimal API, gRPC, worker, Azure Functions)
                      ┌───────────────────────────────────────────┐
  producer ──────────►│ IMessagingTransport  (send / publish /    │
                      │                       sizing)  — unchanged│
                      └───────────────────────────────────────────┘
  ═══════════════════════════ send │ process ═══════════════════════════
                      ┌───────────────────────────────────────────┐
                      │ IMessagingMessageSource  (pull)           │
  broker ────────────►│  ReceiveBatchAsync(queue, maxMessages,    │
    (Storage Queue,   │                    maxWait, ct) -> 0..n   │
     in-memory, SQL)  │  Capabilities: batch size, lock duration, │
                      │  renewal support, long-poll support       │
                      └──────────────────┬────────────────────────┘
                                         │ credits
                      ┌──────────────────▼────────────────────────┐
                      │ MessagingProcessorHost                     │
                      │  receive loop ─► bounded Channel ─► N      │
                      │  workers ─► MessagingDispatcher            │
                      │  + concurrency controller                  │
                      │  + idle backoff                            │
                      │  + shared lock renewer                     │
                      └──────────────────▲────────────────────────┘
                                         │ same delivery callback
                      ┌──────────────────┴────────────────────────┐
  broker ────────────►│ IMessagingNativeProcessor  (push)         │
    (Service Bus via  │  StartProcessingAsync(queue, handler,     │
     ServiceBusProc.) │                       options, ct)        │
                      │ framework runs NO pump; reports effective │
                      │ concurrency for metrics + validation      │
                      └───────────────────────────────────────────┘

  Azure Functions: no seam here at all. Generated triggers + the Functions
  host's own concurrency + the unchanged MessagingDispatcher.
```

Sketch of the seams (final signatures are an `AMF-01` deliverable):

```csharp
/// <summary>Pull-style delivery source with explicit credits and an empty signal.</summary>
public interface IMessagingMessageSource
{
    MessagingReceiverCapabilities ReceiverCapabilities { get; }

    /// <returns>Zero deliveries means "queue empty"; the host backs off.</returns>
    ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
        string queue, int maxMessages, TimeSpan maxWait, CancellationToken ctk);
}

/// <summary>Push-style transports that own their own pump.</summary>
public interface IMessagingNativeProcessor
{
    ValueTask<IMessagingProcessorHandle> StartProcessingAsync(
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery,
        MessagingProcessingOptions options,
        CancellationToken ctk);
}

public sealed record MessagingReceiverCapabilities(
    int MaximumBatchSize,          // 1 for degenerate transports, 32 for Storage Queues
    bool SupportsServerSideWait,   // long poll vs. client polling
    bool SupportsLockRenewal,
    TimeSpan? NativeLockDuration,  // null when unknown/not applicable
    bool OwnsConcurrency);         // true for native processors
```

`IMessagingLockedDelivery` gains `DateTimeOffset? LockedUntil` (needed for
renew-at-50 %) and `string DeliveryId` (needed for renewer bookkeeping and
correlated diagnostics), as required members.

Messaging is pre-release, so this is a clean replacement:
`IMessagingReceiveTransport` and `MessagingReceivePump` are **removed**, not
adapted (§11).

## 6. Processor host runtime

One `MessagingProcessorHost` per participant queue, replacing
`MessagingReceivePump` for pull transports.

### 6.1 Structure

- **Receive loop** (1 by default, `ReceiveChannels` configurable to overlap
  round trips on high-RTT links): computes available credit, calls
  `ReceiveBatchAsync(min(credit, capabilities.MaximumBatchSize), waitWindow)`,
  writes deliveries into the buffer, and updates the backoff state.
- **Bounded `Channel<IMessagingLockedDelivery>`** with
  `BoundedChannelFullMode.Wait`. Capacity = the prefetch budget (§6.3). This
  channel **is** the backpressure mechanism: a full channel means the receive loop
  awaits instead of pulling more locked work off the broker.
- **Worker tasks**: `async` loops (not threads) reading the channel and calling
  the unchanged `MessagingDispatcher.OnDeliveryAsync`. Worker count is the
  controller's current limit; workers are added/removed by starting/cancelling
  tasks, no thread injection.
- **Shared lock renewer**: one timer per host over all in-flight deliveries
  (§6.5), replacing the per-message `Task.Delay` loop.
- **Graceful stop**: stop receiving, drain the buffer within
  `ShutdownTimeout`, abandon what could not be processed (so redelivery is
  immediate rather than lock-expiry-delayed), then stop.

Credit invariant, enforced on every receive decision:

```
inFlight + buffered + requested  ≤  PrefetchBudget
requested                        ≤  ReceiveCapabilities.MaximumBatchSize
```

### 6.2 Adaptive concurrency

Defaults: `InitialConcurrency = Environment.ProcessorCount`, `MinConcurrency = 1`,
`MaxConcurrency = ProcessorCount × 8`, all overridable, and all clamped by the
prefetch budget.

**CPU utilisation is not a control signal.** The bottleneck a processor host must
converge on depends entirely on the handler's work profile:

| Profile | Bottleneck | What happens if concurrency keeps growing |
| --- | --- | --- |
| Compute-bound (parse, transform, compress) | Host CPU | Throughput plateaus at ≈ `ProcessorCount`; extra workers add context switching and GC pressure. Self-limiting and easy to see. |
| I/O-bound (SQL, HTTP, blob) | The dependency's *useful concurrency* | CPU stays near idle, so a CPU-based controller would grow to `MaxConcurrency` unconditionally. Beyond the dependency's saturation point, throughput is flat while per-message latency grows **linearly** with concurrency (Little's law), lock renewals multiply, and the dependency starts shedding load — the host converts its own overload into 429/timeout/dead-letter noise. |

The controller therefore uses **throughput and latency, never CPU**, and needs
three independent brakes so an I/O-bound workload cannot grow without bound:

1. **Throughput gate (AIMD).** Increase only when measured throughput improved by
   more than a noise band (`ThroughputImprovementThreshold`, default 5 %) over the
   previous interval. A saturated dependency yields flat throughput, so growth
   stops on its own.
2. **Latency-gradient guard.** Track `rttNoLoad` — the long-window minimum of the
   observed handler duration, i.e. the duration when the dependency was not
   congested — and `rttShort`, a short-window EWMA. Then
   `gradient = clamp(rttNoLoad / rttShort, 0.5, 1.0)`. Growth requires
   `gradient ≥ GradientIncreaseThreshold` (default 0.9); a sustained
   `gradient < 0.9` means added workers are queueing at the dependency and the
   limit is reduced towards `limit × gradient`. This is the signal that
   distinguishes "the dependency has spare capacity" from "the dependency is
   absorbing our queue", which no throughput reading alone can do.
3. **Little's law cap.** `usefulConcurrency ≈ throughput × rttNoLoad`. The limit is
   hard-capped at `LittlesLawSlack × ceil(usefulConcurrency)` (default slack 2).
   Once the dependency saturates, throughput stops rising, `rttNoLoad` stays put,
   and this cap freezes — the definitive answer to "an I/O-bound handler grows
   parallelism indefinitely".

Controller, evaluated on a fixed interval (default 5 s) — additive increase,
multiplicative decrease:

| Signal | Reaction |
| --- | --- |
| Buffer non-empty **and** throughput gate passed **and** `gradient ≥ 0.9` **and** `limit < Little's-law cap` **and** no adverse signal | `limit += 1` |
| Latency inflation (`gradient < 0.9` for 2 consecutive intervals, no errors) | `limit = max(min, floor(limit × gradient))` — the I/O-bound brake |
| Broker throttling (`ServiceBusFailureReason.ServiceBusy`, HTTP 503/`ServerBusy`) | `limit = max(min, limit / 2)` immediately, plus error backoff |
| Lock lost / renewal failure | `limit = max(min, limit / 2)` and shrink prefetch budget |
| Handler timeout (`MaximumHandlerDuration` hit) | `limit = max(min, limit × 3/4)` |
| Thread-pool starvation (probe task queue delay > threshold) | `limit = max(min, limit / 2)`, never increase while starved |
| Explicit downstream backpressure signal from a handler | `limit = max(min, limit / 2)`; the delivery is abandoned with `RetryDelay` |
| Buffer empty (queue drained) | Hold; do not increase — extra workers cannot help an empty queue |

The explicit signal is a new `MessagingBackpressureException` (or a
`MessagingBackpressureSignal` on the incoming context) that a handler throws when
*its* bottleneck — SQL pool exhaustion, HTTP 429 from a dependency — is the
limit. This is the "local bottleneck backpressure" case from the problem
statement that no metric can infer reliably, and it is the recommended tool when
the dependency's capacity is *known* (a connection pool size, a documented rate
limit): declaring it beats discovering it.

`rttNoLoad` is re-armed periodically (default every 10 minutes) so a permanently
slower dependency does not leave a stale optimistic baseline, and the guard is
suppressed while the buffer is empty (measurements there are meaningless).

> `ponytail:` a bounded AIMD loop plus a gradient guard, not a full Vegas/Gradient2
> controller with probe phases and RTT histograms. Ceiling: it oscillates around
> the optimum, reacts on a 5 s granularity, and a *bimodal* handler mix (fast and
> slow message types on one queue) inflates `rttShort` and can under-shoot the
> limit. Upgrade paths, in order: split the slow message type onto its own queue
> (free, no code), then swap the public `IMessagingConcurrencyController`
> implementation for a percentile-based gradient controller.

### 6.3 Prefetch budget

```
PrefetchBudget = clamp(
    ceil(concurrencyLimit × PrefetchMultiplier),   // default multiplier 2
    concurrencyLimit,
    MaximumPrefetch)                               // default 8 × MaxConcurrency
```

then additionally clamped so that the **expected drain time** of a full buffer,
`buffered / (concurrency / EWMA(handlerDuration))`, stays below
`LockSafetyFactor × NativeLockDuration` (default 0.5) whenever the transport
cannot renew locks, and below `MaximumHandlerDuration` budget checks when it can.
The budget is recomputed whenever the concurrency limit changes.

This is the direct answer to Rebus's prefetch-disables-renewal compromise: keep
renewal on **and** bound the buffer.

### 6.4 Idle backoff

Three separate waits, following Rebus's split but with finer granularity:

| Situation | Wait |
| --- | --- |
| `ReceiveBatchAsync` returned 0 | Exponential with full jitter: `MinPollInterval` (default 50 ms) → `MaxPollInterval` (default 5 s), doubling per consecutive empty result. Reset to minimum on the first non-empty batch **for that receive loop** (not globally). |
| No credit available | Do not poll at all; await channel capacity. No timer, no wasted call. |
| Transport error | Fixed cooldown (default 10 s, jittered), independent of the empty-backoff state, with structured logging. |

For transports with `SupportsServerSideWait = true` (Service Bus), backoff grows
the **server-side wait window** (`maxWait`, up to `MaxPollInterval`) instead of
sleeping between calls: the broker holds the request open, so latency stays low
while the call rate stays at 1/window. For Storage Queues, which has no long
poll, the backoff is a real sleep and directly reduces billed transactions —
worst-case idle cost drops from 4 req/s to 0.2 req/s per queue.

### 6.5 Lock renewal

Replaces `MessagingDispatcher._renewLockAsync`:

- **One renewer per host**, a single timer scanning in-flight deliveries, batching
  renewals with `Task.WhenAll`. Cost is O(1) timers instead of O(in-flight).
- Renew when `now ≥ lockedUntil − max(RenewalSafetyMargin, (lockedUntil − acquiredAt) / 2)`
  — i.e. at ~50 % of remaining lock, the same rule Rebus uses, but driven by the
  transport-reported `LockedUntil` rather than a hard-coded 15 s.
- Renewal starts when the delivery **enters the buffer**, not when the handler
  starts. This is what makes prefetch safe.
- Renewal failure cancels that delivery's handler token (as Rebus does), records
  `messaging.process.lock_renewals{outcome=failure}`, and feeds the concurrency
  controller.
- Transports that cannot renew (`SupportsLockRenewal = false`) get a startup
  validation instead: `MaximumHandlerDuration + expected buffer wait` must fit in
  the visibility window, otherwise composition fails fast.
- Storage Queues renewal must be serialised against settlement, because
  `UpdateMessage` rotates the pop receipt that `DeleteMessage` needs
  (`StorageQueueMessagingTransport.cs:382-408` mutates `_popReceipt` with no
  synchronisation — a latent race that becomes reachable the moment renewal and
  settlement run on different tasks).

### 6.6 Settlement

Unchanged rules (`MessagingSettlement.Decide`), but settlement now runs **on the
worker task**, off the receive path, and releases credit only after the settle
call completes. No batch settle: the SDKs do not offer one.

## 7. Transport profiles

| Transport | Seam | Notes |
| --- | --- | --- |
| **Azure Service Bus** | `IMessagingNativeProcessor` over `ServiceBusProcessor` (default), `IMessagingMessageSource` over `ReceiveMessagesAsync` (opt-in, and used by the conformance suite) | `AutoCompleteMessages = false`, `ReceiveMode = PeekLock`, `MaxConcurrentCalls` ← concurrency limit, `PrefetchCount` ← prefetch budget, `MaxAutoLockRenewalDuration` ← `MaximumHandlerDuration + margin`. Adaptive concurrency is applied by stopping/restarting the processor at a new limit (the SDK's limit is immutable per processor) — hysteresis (min dwell time, default 30 s) prevents churn. For very high rates, N processors over N `ServiceBusClient` instances (separate AMQP connections) instead of one processor with a huge limit. |
| **Azure Storage Queues** | `IMessagingMessageSource` | `MaximumBatchSize = 32`, `SupportsServerSideWait = false`, `SupportsLockRenewal = true` (via `UpdateMessage`), `NativeLockDuration` = configured visibility timeout. Biggest single win in the whole PRD: 32× fewer receive transactions. Poison-queue handling stays as-is. |
| **In-memory** | `IMessagingMessageSource` | Must implement batch + empty-return honestly so tests exercise the same code path as production, and must support a fake `IClock` so the controller and backoff are deterministically testable. |
| **Azure Functions** | None — host-owned | `OwnsConcurrency = true`; the framework registers **no** hosted service and **fails startup** if a processor host is composed. Sending/publishing via `IBus` is unchanged, so a Function app is a first-class producer with zero changes. Guide documents `host.json` (`maxConcurrentCalls`, `prefetchCount`, `dynamicConcurrency`) as the tuning surface, and that `MaximumHandlerDuration` must fit the function timeout. |

Rebus is deliberately absent from this table: it is a separate alternative stack,
not a transport this runtime drives (§3).

## 8. Resource provisioning

Throughput-shaping intent is declared where the host already declares its
transport, and is applied only at **create** time where the provider requires it:

```csharp
services.AddMessaging(m => m
    .UseTransport(t => t.UseServiceBus(client, o =>
    {
        o.EnablePartitioning = true;   // create-time only, immutable afterwards
        o.LockDuration = TimeSpan.FromMinutes(2);
    }))
    .Receiver<OrdersParticipant>(r => r.Processing(p =>
    {
        p.MaxConcurrency = 64;
        p.PrefetchMultiplier = 3;
    })));
```

The same options bind from configuration (`IOptions<MessagingProcessingOptions>`
/ `IOptions<ServiceBusMessagingOptions>`) so a deployment can tune a host without
a rebuild, and are surfaced on `MessagingResourceManifest` for the provisioning
path:

- `Partitioned` (bool, default false) → `CreateQueueOptions.EnablePartitioning`.
  Immutable: the reconciler must compare and, on mismatch, throw a diagnostic that
  names the entity, both values, and the fact that recreation is the only fix.
  Premium namespaces must additionally warn that partitioning is a namespace-level
  creation-time choice.
- `LockDuration` (default 60 s) → `CreateQueueOptions.LockDuration`, validated
  against `MaximumHandlerDuration` and the renewal capability. Mutable, so the
  reconciler may update it.
- `MaxSizeInMegabytes` / `MaxMessageSizeInKilobytes` where the tier allows.
- Storage Queues: no provisioning knobs beyond what exists; visibility timeout is a
  client-side receive parameter and stays in options.

Partitioning is opt-in because of its cost: no cross-partition transactions or
send-batches, `SessionId` becomes the partition key, and ordering/dedup are
per-partition only.

## 9. Configuration surface

Transport-neutral, fluent, layered `participant declaration → composition →
transport clamp`, reachable both from the transport declaration shown in §8 and
from runtime setup/configuration binding:

| Option | Default | Meaning |
| --- | --- | --- |
| `MaxConcurrency` | `ProcessorCount × 8` | Hard ceiling for the controller. |
| `InitialConcurrency` | `ProcessorCount` | Starting limit. |
| `MinConcurrency` | 1 | Floor under backpressure. |
| `AdaptiveConcurrency` | `true` | `false` pins the limit at `InitialConcurrency`. |
| `ThroughputImprovementThreshold` | 0.05 | Noise band below which throughput is "not improving". |
| `GradientIncreaseThreshold` | 0.9 | Latency-gradient floor required to grow (§6.2). |
| `LittlesLawSlack` | 2 | Multiplier over the estimated useful concurrency. |
| `PrefetchMultiplier` | 2 | Buffer size relative to concurrency. |
| `MaximumPrefetch` | `8 × MaxConcurrency` | Absolute buffer cap. |
| `LockSafetyFactor` | 0.5 | Fraction of lock duration the buffer may consume. |
| `ReceiveChannels` | 1 | Parallel receive loops per queue. |
| `MinPollInterval` / `MaxPollInterval` | 50 ms / 5 s | Idle backoff bounds. |
| `ErrorCooldown` | 10 s | Transport-error wait. |
| `ShutdownTimeout` | 30 s | Drain window before abandoning in-flight work. |
| `AdvancedMetrics` | `false` | Enables the diagnostic instrument set (§10). |

Every option is validated at composition time against the transport's
`MessagingReceiverCapabilities` and the participant's `IMessagingRetryPolicy`;
impossible combinations fail startup with a named diagnostic rather than
degrading silently at 3 a.m.

## 10. Observability

Instruments split into two tiers, because an operations signal and a tuning
signal have different audiences, different cardinality budgets and different
lifetimes.

### 10.1 Operational tier — always on

On the existing meter `Ark.MediatorFramework.Messaging`
(`OpenTelemetryProcessingMetricsStep.MeterName`), tagged with
`messaging.system`, `messaging.destination.name`, `ark.participant`. These are
the signals worth alerting and autoscaling on; the set is deliberately small so
every host can afford to export it permanently.

| Instrument | Kind | Why it is operational |
| --- | --- | --- |
| `messaging.process.concurrency.limit` | UpDownCounter/gauge | Reliability signal: a limit collapsing towards `MinConcurrency` means the host is being throttled or its dependency is failing. |
| `messaging.process.in_flight` | UpDownCounter/gauge | Saturation signal; with queue depth it is the autoscaling input. |
| `messaging.process.buffered` | UpDownCounter/gauge | Backlog held locally; also an autoscaling input and the scale-in safety check. |
| `messaging.process.throttled` | Counter | Broker throttling — capacity alarm, tier-upgrade trigger. |
| `messaging.process.lock_renewals` | Counter (`outcome`) | Renewal failures precede redelivery and duplicate work; the prefetch safety alarm. |

Existing instruments (`messaging.client.operation.duration`,
`messaging.process.duration`, `messaging.message.time_in_queue`,
`messaging.process.messages`, `messaging.process.attempts`) are unchanged and
stay in this tier.

### 10.2 Advanced tier — opt-in

On a separate meter `Ark.MediatorFramework.Messaging.Advanced`
(`MessagingMetrics.AdvancedMeterName`), with an `adv.` name segment so the tier
is obvious in any dashboard or metric browser. These answer "why is the limit
where it is" during tuning or an incident, not "is the system healthy".

| Instrument | Kind | Purpose |
| --- | --- | --- |
| `messaging.adv.receive.batch.size` | Histogram | Effective batch sizes — proves batching works. |
| `messaging.adv.receive.empty` | Counter | Empty receives — backoff/cost review. |
| `messaging.adv.receive.backoff.interval` | Histogram | Current idle wait. |
| `messaging.adv.process.queue_wait` | Histogram | Fetch → handler start; the buffer's latency contribution. |
| `messaging.adv.process.settle.duration` | Histogram | Settlement cost, currently invisible. |
| `messaging.adv.concurrency.gradient` | Histogram | Latency gradient driving the I/O-bound brake (§6.2). |
| `messaging.adv.concurrency.decision` | Counter (`reason`) | Why the limit moved: `throughput`, `gradient`, `throttled`, `lock_lost`, `timeout`, `starvation`, `backpressure`. |

Recording is gated on `MessagingProcessingOptions.AdvancedMetrics` so the
measurement cost is not paid when nobody is listening, and registration is a
separate OTel extension:

```csharp
builder.Services.AddArkOTel()
    .WithMetrics(m => m
        .AddArkMessagingInstrumentation()            // operational tier
        .AddArkMessagingAdvancedInstrumentation());  // opt-in, tuning/debug
```

The advanced extension also flips `AdvancedMetrics` on, so a host cannot register
the meter and then wonder why it is empty.

## 11. Breaking changes

MediatorFramework Messaging is **not yet released**, so this is a replacement,
not a migration. No adapter, no obsolete shims, no default-interface hedges to
carry forever:

- `IMessagingReceiveTransport` and `MessagingReceivePump` are **removed**,
  replaced by `IMessagingMessageSource` / `IMessagingNativeProcessor` and
  `MessagingProcessorHost`.
- `IMessagingLockedDelivery.LockedUntil` and `.DeliveryId` are **required**
  members; every transport implements them.
- `MessagingDispatcher` loses its `lockRenewalInterval` constructor parameter and
  its internal renewal loop; renewal belongs to the host renewer. Its
  per-delivery semantics — settlement, retries, scoping — are unchanged.
- Default behaviour changes: a receiver host that ran at concurrency 1 now runs at
  `ProcessorCount` and adapts from there. `AdaptiveConcurrency = false` +
  `InitialConcurrency = 1` restores the old shape exactly, and the guide documents
  that escape hatch.
- Azure Functions receivers are untouched by all of the above, and so is every
  producer: `IBus` and `IMessagingTransport` keep their current shape.
- Existing tests and samples that compose the old pump are updated in the same
  task that removes it; API surface baselines (`ApiSurfaceGenerator` snapshots)
  and `packages.lock.json` files are updated per task.

## 12. Testing

- **Conformance** (`MessagingTransportConformanceTests`, run against in-memory,
  Storage Queues and the Service Bus emulator): batch receive honours
  `maxMessages`; an empty queue returns an empty batch within `maxWait`; credits
  are never exceeded; renewal extends a lock past its original expiry; settlement
  after renewal succeeds (the pop-receipt race); concurrent settle of a batch
  leaves the queue empty.
- **Deterministic runtime tests** with a fake `IClock` and a scripted source:
  backoff doubles and resets correctly; the controller increases only on improving
  throughput; each adverse signal halves the limit; the channel blocks the receive
  loop when full; shutdown drains then abandons.
- **I/O-bound convergence test**: a scripted handler backed by a simulated
  dependency with a fixed useful concurrency `K` and a queueing delay beyond it.
  The controller must stabilise near `K` and must **not** climb to
  `MaxConcurrency`, proving the gradient guard and the Little's-law cap work when
  CPU stays idle. Repeated with `K` changing mid-run (baseline re-arming).
- **Failure injection**: lock-lost mid-handler, throttling responses, renewal
  failures, handler timeouts — assert settlement decisions are unchanged from
  today.
- **Throughput smoke test** (`TestCategory("integration")`, emulator, not a CI
  gate): 10 000 trivial messages must complete at ≥ 10× the current sequential
  baseline with zero lock-lost events.
- Existing messaging tests are **updated, not preserved** where they compose the
  removed pump (§11); their assertions on settlement, retries and scoping must
  survive verbatim.
- No new test framework or dependency; MSTest + AwesomeAssertions as today.

## 13. Success criteria

1. A trivial handler on an 8-vCPU host processes ≥ 2 000 msg/s from Service Bus and
   ≥ 1 000 msg/s from Storage Queues, versus ~20–60 msg/s today.
2. CPU utilisation under sustained backlog exceeds 80 % for a CPU-bound handler.
3. For an I/O-bound handler against a dependency with useful concurrency `K`, the
   limit stabilises within `[K, LittlesLawSlack × K]` and never reaches
   `MaxConcurrency`, with no framework-induced 429/timeout storm.
4. Zero `MessageLockLost` events during a 30-minute sustained load run.
5. Idle cost: ≤ 0.2 receive requests/second per idle Storage Queue.
6. A saturated downstream (throttled dependency) causes the limit to fall and
   stabilise, with no dead-lettering caused by the framework itself.
7. Azure Functions receivers and all producers build and behave identically
   before and after the change.
8. The operational metric tier alone is sufficient to build a scale-out rule; no
   advanced instrument is required for autoscaling.

## 14. Risks

| Risk | Mitigation |
| --- | --- |
| Higher concurrency exposes handler thread-safety bugs that the sequential pump hid. | Opt-out documented; release note calls it out explicitly; per-delivery DI scope is already isolated. |
| Prefetch increases redelivery on crash (locked messages wait for lock expiry). | Bounded buffer, graceful drain-then-abandon on shutdown. |
| The controller oscillates or fights an external autoscaler. | Hysteresis + min dwell time; `AdaptiveConcurrency = false` escape hatch; metrics expose the limit. |
| Restarting `ServiceBusProcessor` to change `MaxConcurrentCalls` costs a stall. | Min dwell time (30 s) and step changes only; alternatively pin the processor and adapt via a framework-side semaphore. |
| Partitioning misconfiguration is unfixable in place. | Loud reconciler diagnostic naming the entity and the required recreation. |
| Scope creep into ordering/sessions. | Explicit non-goal. |

## 15. Rejected approaches

### 15.1 Rejected: keep `IAsyncEnumerable` and wrap it in `Parallel.ForEachAsync`

Superficially a one-line fix. It gives no credit accounting (the enumerable pulls
as fast as the broker allows), no empty signal, no batch boundary, no way to bound
in-flight locked messages against the lock duration, and its cancellation
semantics abandon the enumerator mid-flight, leaking locked deliveries. It also
cannot express "stop pulling because *my* downstream is saturated".

### 15.2 Rejected: copy Rebus's worker model verbatim

Thread-per-worker plus a fire-and-forget continuation costs a dedicated OS thread
per worker for no benefit on modern async transports; `MaxParallelism` is fixed at
construction, so nothing adapts; a parallelism slot is held during idle backoff by
design; and receive is one message per broker round trip. Rebus's *good* ideas
(empty signal, three-way backoff split, shared limiter, 50 % lock renewal) are
adopted; its structure is not.

### 15.3 Rejected: prefetch **or** lock renewal (Rebus's `EnablePrefetching` behaviour)

Disabling renewal whenever prefetch is on trades one failure mode for another: a
prefetched message whose lock expires in the local buffer is redelivered and
processed twice. Bounding the buffer by measured drain time (§6.3) keeps both.

### 15.4 Rejected: writing our own Service Bus pump instead of `ServiceBusProcessor`

The SDK's processor already implements credit refill, concurrent dispatch,
automatic lock renewal, error handling and graceful stop, and is maintained by
Azure. Reimplementing it means owning AMQP link credit semantics forever. The
pull seam is still implemented for Service Bus, but only as the conformance/testing
path and for hosts that want a single unified runtime.

### 15.5 Rejected: batch settlement

`Azure.Messaging.ServiceBus` exposes `CompleteMessageAsync` per message;
`Azure.Storage.Queues` exposes `DeleteMessageAsync` per message. There is no batch
settle API to call. Concurrency, not batching, is what removes settle latency from
the critical path.

### 15.6 Rejected: per-message-type concurrency limits

Attractive ("let the slow report generator use 2 workers, the fast audit writer
64") but the limit must be applied *before* the payload is deserialized, i.e.
before the message type is reliably known for all codecs, and it fragments the
credit accounting that keeps locks safe. Per-participant (per-queue) concurrency,
with separate participants for separate workloads, achieves the same isolation
with none of the complexity.

### 15.7 Rejected: automatic partition keys derived from message type

Would silently change ordering and dedup semantics and break cross-entity
transactional sends. Partitioning stays an explicit, documented, create-time
decision.

### 15.8 Rejected: latency as the *sole* saturation signal, and CPU as a signal at all

Two symmetric mistakes. CPU utilisation cannot drive the controller because an
I/O-bound handler leaves the CPU idle at every concurrency level, so a CPU rule
grows to `MaxConcurrency` unconditionally. Raw latency cannot drive it alone
either: handler latency rises for many innocent reasons (larger payloads, cold
caches, GC), so cutting on latency causes false negatives under legitimate load.
The design uses latency only in *ratio* form (`rttNoLoad / rttShort`, §6.2),
which cancels the workload-dependent baseline, and only as a **growth veto plus a
proportional reduction** — the hard cuts still come from unambiguous signals
(throttling responses, lock loss, thread-pool starvation) or from the explicit
`MessagingBackpressureException`.

### 15.9 Rejected: one flat metric set

The first draft emitted ten new instruments on one meter. Half are incident and
autoscaling signals with a permanent audience; half are tuning aids read once
during a load test. Exporting them together forces every host to pay for
histograms it will never look at, and buries the throttling alarm among debug
counters. Splitting into an always-on operational meter and an opt-in advanced
meter (§10) costs one extra registration call and keeps both sets honest.

## 16. Delivery

Proposed `AMF` (Ark Messaging Framework) task series, in implementation order,
tracked on [the task board](progress/tasks/README.md). Each task must leave the
solution building and green, update the messaging guide section it affects, and
extend `Ark.MediatorFramework.Sample` where it changes runtime behaviour — the
existing AZM task rules.

| Task | Title | Depends on |
| --- | --- | --- |
| [`AMF-01`](progress/tasks/messaging/AMF-01-receive-seam-split.md) | Receive seam split: `IMessagingMessageSource`, `IMessagingNativeProcessor`, `MessagingReceiverCapabilities`, delivery `LockedUntil`/`DeliveryId`; old receive seam removed | — |
| [`AMF-02`](progress/tasks/messaging/AMF-02-processor-host.md) | `MessagingProcessorHost`: bounded channel, credit accounting, worker pool, graceful drain | `AMF-01` |
| [`AMF-03`](progress/tasks/messaging/AMF-03-adaptive-backoff.md) | Idle/error/no-capacity backoff with server-side-wait growth | `AMF-02` |
| [`AMF-04`](progress/tasks/messaging/AMF-04-shared-lock-renewer.md) | Shared lock renewer driven by native lock duration; dispatcher renewal loop retired | `AMF-02` |
| [`AMF-05`](progress/tasks/messaging/AMF-05-adaptive-concurrency.md) | Adaptive concurrency controller, I/O-bound gradient guard, `MessagingBackpressureException` | `AMF-02`, `AMF-04` |
| [`AMF-06`](progress/tasks/messaging/AMF-06-storage-queue-batch-receive.md) | Storage Queues batch receive (32), renewal/settle race fix, adaptive visibility | `AMF-01`–`AMF-04` |
| [`AMF-07`](progress/tasks/messaging/AMF-07-service-bus-native-processor.md) | Service Bus `ServiceBusProcessor` native pump + pull batch path + multi-client fan-out | `AMF-01`–`AMF-05` |
| [`AMF-08`](progress/tasks/messaging/AMF-08-throughput-provisioning-options.md) | Throughput options on the transport declaration: partitioning, lock duration, reconciler mismatch diagnostics | `AMF-07` |
| [`AMF-09`](progress/tasks/messaging/AMF-09-metrics-guide-and-smoke-test.md) | Two-tier metrics + OTel opt-in extension, guide, sample tuning walkthrough, throughput smoke test, API surface baseline | all |

## 17. Open decisions

| # | Decision | Recommendation |
| --- | --- | --- |
| AMF-D1 | Default `MaxConcurrency` | `ProcessorCount × 8`, adaptive from `ProcessorCount`. Conservative alternative: `ProcessorCount`. |
| AMF-D2 | Service Bus adaptive concurrency mechanism | Restart the processor at a new `MaxConcurrentCalls` with 30 s hysteresis. Alternative: fixed processor limit + framework-side semaphore (no stalls, wastes prefetch credits). |
| AMF-D3 | Whether `Partitioned` defaults to true for new queues | No — opt-in, because of the transaction/session constraints. |
| AMF-D4 | Whether the gradient guard is on by default | Yes — an I/O-bound workload is the common case, and the guard only vetoes growth. `AdaptiveConcurrency = false` disables the whole controller for hosts that want a fixed limit. |
| AMF-D5 | Whether the backoff table is configurable Rebus-style (`TimeSpan[]`) | No — two bounds plus jitter, which is simpler and covers every observed case. |

## 18. References

- Ark: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/` —
  `IMessagingTransport.cs`, `MessagingReceivePump.cs`, `MessagingDispatcher.cs`,
  `ServiceBusMessagingTransport.cs`, `StorageQueueMessagingTransport.cs`,
  `ServiceBusTransportManagement.cs`, `OpenTelemetryProcessingMetricsStep.cs`.
- Ark design context: [`progress/azure-functions-messaging-design.md`](progress/azure-functions-messaging-design.md),
  [`progress/tasks/azure-functions/AZM-05-transport-abstraction-and-inmemory.md`](progress/tasks/azure-functions/AZM-05-transport-abstraction-and-inmemory.md),
  [`progress/tasks/azure-functions/AZM-09-dispatch-retry-and-failure.md`](progress/tasks/azure-functions/AZM-09-dispatch-retry-and-failure.md),
  [`progress/future-improvements.md`](progress/future-improvements.md).
- Rebus: `Rebus/Transport/ITransport.cs`, `Rebus/Transport/ITransactionContext.cs`,
  `Rebus/Workers/ThreadPoolBased/{ThreadPoolWorker,ThreadPoolWorkerFactory,IBackoffStrategy,DefaultBackoffStrategy}.cs`,
  `Rebus/Threading/ParallelOperationsManager.cs`, `Rebus/Config/Options.cs`,
  `Rebus.AzureServiceBus/AzureServiceBus/AzureServiceBusTransport.cs`,
  `Rebus.AzureServiceBus/Internals/MessageLockRenewer.cs`,
  `Rebus.AzureServiceBus/Config/AzureServiceBusTransportSettings.cs`.
- Azure SDK: `Azure.Messaging.ServiceBus` `ServiceBusProcessorOptions`,
  `ServiceBusReceiverOptions`; `Azure.Storage.Queues` `QueueClient.ReceiveMessagesAsync`.
- MassTransit: `src/Transports/MassTransit.Azure.ServiceBus.Core/` —
  `ServiceBusConnectionContext`, `BaseClientSettings`, `IServiceBusEndpointConfigurator`.
- Microsoft Learn: [Service Bus partitioned entities](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-partitioning),
  [Service Bus prefetch](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-prefetch),
  [Storage Queues scalability targets](https://learn.microsoft.com/azure/storage/queues/scalability-targets),
  [Azure Functions Service Bus host.json options](https://learn.microsoft.com/azure/azure-functions/functions-bindings-service-bus).
