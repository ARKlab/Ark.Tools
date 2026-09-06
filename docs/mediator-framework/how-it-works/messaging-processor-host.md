# Messaging processor host mechanics

How `MessagingProcessorHost` fetches, buffers, renews, and paces work. Read
[Messaging processing](../guide/messaging-processing.md) first: it covers how to
run a processor host and which setting to change for which symptom.

## Two seams, deliberately separate

- **Send** — `IMessagingTransport` (`SendAsync`, `PublishAsync`, sizing). Used by
  every host, including Azure Functions, and unaffected by the processing side.
- **Process** — `IMessagingMessageSource`, a *pull* seam:
  `ReceiveBatchAsync(queue, maxMessages, maxWait, ctk)` returns zero or more
  locked deliveries, never more than `maxMessages`. An **empty result means "the
  queue is empty"**, is returned after at most `maxWait`, is not an error, and
  never means "the source ended". The source does not sleep on an empty batch:
  deciding how long to wait before the next call belongs to the host.

Each source declares `ReceiverCapabilities` — a `MessagingReceiverCapabilities`
record of maximum batch size, server-side wait support, lock renewal support and
native lock duration — read once at composition time so a host adapts instead of
guessing. Every delivery carries a required `DeliveryId` and a `LockedUntil`
instant for lock-aware renewal.

Azure Functions receivers never touch `IMessagingMessageSource`: they keep their
generated triggers and the Functions host's own concurrency. Composing a
processor host inside a Functions host fails startup with the
`ProcessorHostInTriggeredHost` diagnostic, and composing one over a transport
that is not a message source fails with `TransportIsNotAMessageSource`.

## Four cooperating parts

- a **receive loop** that acquires credit, calls `ReceiveBatchAsync` with
  `min(credit, ReceiverCapabilities.MaximumBatchSize)` and writes deliveries into
  the buffer;
- a **bounded buffer** (`Channel<IMessagingLockedDelivery>` with
  `BoundedChannelFullMode.Wait`) sized to the hard prefetch ceiling, because a
  bounded channel cannot be resized while the host runs; the *current* budget is
  enforced by credits plus a debt counter, not by the channel;
- a **worker pool** dispatching through the unchanged `MessagingDispatcher`, with
  its existing per-delivery scope, settlement and retry semantics;
- a **credit accountant** enforcing `inFlight + buffered + requested ≤
  PrefetchBudget`.

Credit is released **after settlement**, not after the handler returns, so a slow
settle cannot cause over-fetching. When the budget is exhausted the receive loop
blocks on credit and makes no broker call at all.

The prefetch budget is `clamp(ceil(limit × PrefetchMultiplier), limit,
MaximumPrefetch)`. When the transport cannot renew locks it is additionally
clamped so a full buffer is expected to drain within `LockSafetyFactor ×
NativeLockDuration`, using `ExpectedHandlerDuration` as the per-message estimate.
`MaximumPrefetch` defaults to eight times `MaximumConcurrency`; a value below
`MaximumConcurrency` fails composition with `ProcessingOptionsInvalid`.

Shutdown is stop-receiving → drain → abandon: the receive loop is cancelled
first so the buffer can only shrink, workers finish in-flight and buffered work
within `ShutdownTimeout`, then anything still unprocessed is abandoned so
redelivery is immediate rather than lock-expiry-delayed. Deliveries already
fetched from the broker always reach the buffer, so cancelling the receive loop
never drops a delivery un-settled.

## Idle, error and no-capacity waits

The receive loop has three independent waits, and they never share state:

- **Empty result** — exponential backoff with full jitter, doubling from
  `MinPollInterval` to `MaxPollInterval` per consecutive empty result and
  resetting on the first non-empty batch. Full jitter (`random(0, cap)`) keeps
  replicas of the same host from polling in lockstep.
- **No credit** — no timer at all. The loop awaits credit, so a saturated host
  issues no broker call; nothing is polled while nothing can be accepted.
- **Transport error** — a fixed jittered `ErrorCooldown`, sampled in its upper
  half, with a structured warning naming the queue and the failure. An error
  never lengthens the empty backoff, and an empty result never shortens the
  cooldown.

When the source declares `SupportsServerSideWait` (Service Bus), backoff grows
the `maxWait` passed to `ReceiveBatchAsync` up to `MaxPollInterval` instead of
sleeping: an idle receiver holds one long request rather than polling, and the
first message after an idle period is delivered without waiting out a client
sleep. Sources without server-side wait (Storage Queues) return immediately and
the host sleeps the jittered interval, which takes an idle queue from roughly
four requests per second down to a fraction of one. The transport itself never
sleeps: it waits only as instructed by `maxWait`.

## Shared lock renewal

Every host owns a single lock renewer, not one timer per delivery. A delivery is
registered the moment it enters the prefetch buffer — not when a worker picks it
up — so a message that waits behind the concurrency limit keeps its lock alive
instead of expiring in the buffer. This registration point is what makes
prefetch safe.

The renewer wakes on a fixed `RenewalScanInterval`, renews at most
`MaximumRenewalBatch` locks per scan, and treats a lock as due when

```
now >= lockedUntil - max(RenewalSafetyMargin, (lockedUntil - acquiredAt) / 2)
```

so a long lock is renewed at roughly its halfway point while a short lock still
gets the full `RenewalSafetyMargin` of headroom. Renewal is serialised against
settlement: a renewal never overlaps a complete, abandon or dead-letter on the
same delivery, which matters for Storage Queues where renewing rotates the pop
receipt that settlement needs. When a renewal fails, the delivery's handler token
is cancelled — the lock is gone, and continuing to work on it would only produce
a duplicate.

Transports that cannot renew (`SupportsLockRenewal == false`) are validated at
composition time instead of at runtime: if `MaximumHandlerDuration` plus the
worst-case buffer wait implied by `PrefetchBudget`, `Concurrency` and
`ExpectedHandlerDuration` exceeds the transport's `NativeLockDuration`, the
composition fails with `ProcessingOptionsInvalid` rather than letting locks
expire in production.

## Adaptive concurrency

The concurrency limit adapts on a fixed `ConcurrencyEvaluationInterval`.
**CPU utilisation is never a signal**: it says nothing about an I/O-bound
handler, whose bottleneck is the dependency's useful concurrency, not the host.
Growth therefore needs three independent permissions:

- **Throughput gate** — measured throughput must beat the previous interval by
  more than `ThroughputImprovementThreshold`. A saturated dependency yields flat
  throughput, so growth stops by itself.
- **Latency gradient** — `gradient = clamp(rttNoLoad / rttShort, 0.5, 1.0)`, where
  `rttNoLoad` is the long-window minimum handler duration and `rttShort` a short
  EWMA. Growth requires `gradient >= GradientIncreaseThreshold`; two consecutive
  intervals below it reduce the limit to `floor(limit x gradient)` even when
  nothing failed. The ratio cancels the workload-dependent baseline that makes
  raw latency useless as a signal. `rttNoLoad` is re-armed every
  `BaselineRearmInterval` so a permanently slower dependency does not leave a
  stale optimistic baseline.
- **Little's law cap** — `usefulConcurrency = throughput x rttNoLoad`, and the
  limit is hard-capped at `LittlesLawSlack x ceil(usefulConcurrency)`. Once the
  dependency saturates this cap freezes: it is what stops an I/O-bound workload
  from growing to `MaximumConcurrency`.

Decreases are multiplicative and immediate — waiting for the next interval means
another interval of the overload that caused them:

| Signal | Reaction |
| --- | --- |
| Broker throttling | `limit / 2` |
| Lock lost or renewal failure | `limit / 2` |
| Handler timeout (`MaximumHandlerDuration`) | `limit x 3/4` |
| Thread-pool starvation (probe delay > `ThreadPoolStarvationThreshold`) | `limit / 2`, and no growth while starved |
| `MessagingBackpressureException` | `limit / 2`, delivery abandoned for retry |
| Buffer empty | hold — extra workers cannot help a drained queue |

The worker pool follows the limit cooperatively. Growth starts workers
immediately; a shrink is applied only after two consecutive intervals below the
current pool size, and a worker leaves after its current delivery settles — never
mid-flight. The prefetch budget is recomputed from the new limit at the same
time. Whatever the controller decides, the limit stays inside
`[MinimumConcurrency, MaximumConcurrency]` and the prefetch clamp.

`AdaptiveConcurrency = false` pins the limit at `InitialConcurrency` and disables
all measurement work.
