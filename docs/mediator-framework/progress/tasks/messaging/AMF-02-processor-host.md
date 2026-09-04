# AMF-02 — `MessagingProcessorHost`: bounded buffer, credits and worker pool

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-01
**Scope**: FRAMEWORK
**Design**: [Processor host runtime](../../../messaging-throughput-prd.md#6-processor-host-runtime), [Prefetch budget](../../../messaging-throughput-prd.md#63-prefetch-budget)

## Problem

The removed pump processed one message at a time because receive, dispatch and
settle were a single `await` chain. A processor host must decouple those stages
so the broker round trip, the handler and the settle call overlap, while never
holding more locked deliveries than it can safely process.

## Execution map

- **Receive loop**: computes available credit, calls `ReceiveBatchAsync` with
  `min(credit, capabilities.MaximumBatchSize)`, writes deliveries to the buffer.
- **Bounded buffer**: a `Channel<IMessagingLockedDelivery>` with
  `BoundedChannelFullMode.Wait`, capacity = prefetch budget. Backpressure is the
  channel refusing writes, not a manual counter.
- **Worker pool**: N async workers reading the channel and calling the unchanged
  `MessagingDispatcher.OnDeliveryAsync`. Async tasks, not dedicated threads.
- **Credit invariant**: `inFlight + buffered + requested ≤ PrefetchBudget`,
  enforced at every receive decision, with `requested ≤ MaximumBatchSize`.
- **Prefetch budget**: `clamp(ceil(limit × PrefetchMultiplier), limit,
  MaximumPrefetch)`, additionally clamped so expected full-buffer drain time stays
  under `LockSafetyFactor × NativeLockDuration`.
- **Graceful drain**: on stop, stop receiving, let in-flight work finish within
  `ShutdownTimeout`, then abandon what remains so redelivery is immediate rather
  than lock-expiry-delayed.
- **Native path**: for `IMessagingNativeProcessor` transports the host starts the
  native processor and runs no loop, no channel and no workers.
- **Boundary**: fixed concurrency at this task. Backoff is AMF-03, renewal AMF-04,
  adaptivity AMF-05.

## Implementation steps

1. Add `MessagingProcessorHost`, one per participant queue, as the hosted service
   registered by the receiver composition.
2. Implement the credit accounting as a single owner (the receive loop) so the
   invariant has one writer and needs no lock.
3. Implement the worker loop over `ChannelReader.ReadAllAsync`, dispatching
   through the existing `MessagingDispatcher` with its existing per-delivery DI
   scope.
4. Move settlement onto the worker task and release credit only after settlement
   completes.
5. Implement `StopAsync` as stop-receiving → drain → abandon-remaining, honouring
   `ShutdownTimeout` and the host cancellation token.
6. Compute and recompute the prefetch budget whenever the concurrency limit
   changes, and validate it against the transport's `NativeLockDuration`.
7. Route native-processor transports to `StartProcessingAsync` and surface the
   handle's effective concurrency for validation and metrics.
8. Add composition-time validation with named diagnostics for impossible option
   combinations.

## Core code shapes

The channel is the only backpressure mechanism: a full buffer blocks the receive
loop, which is exactly the desired behaviour and needs no extra signalling.

Credit is released after settlement rather than after handler return, so a slow
settle cannot cause the host to over-fetch.

## Guide contribution

Document the host structure, the credit invariant, the prefetch budget formula,
the shutdown sequence, and how the native path differs from the pull path.

## Sample extension

Run the sample's processor host on the new runtime with default options and
document the observed concurrency in the sample readme.

## Required test coverage

- The credit invariant holds under a scripted source that always has backlog.
- A full buffer blocks the receive loop and no extra receive call is made.
- Workers dispatch concurrently: N slow handlers overlap rather than serialise.
- Settlement runs on the worker and credit is released only afterwards.
- Shutdown drains in-flight work, then abandons the remainder within
  `ShutdownTimeout`.
- A native-processor transport starts no receive loop and no workers.
- Settlement, retry and scoping semantics are byte-for-byte the previous ones.

## Outcomes

- A processor host processes many messages concurrently with bounded, lock-safe
  buffering.
- Shutdown no longer relies on lock expiry for redelivery.

## Acceptance

- [ ] `MessagingProcessorHost` implements receive loop, bounded channel, worker pool and drain.
- [ ] The credit invariant is enforced and tested.
- [ ] Prefetch budget derives from concurrency and lock duration.
- [ ] Native-processor transports bypass the pull runtime entirely.
- [ ] The [task board](../README.md) status for AMF-02 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
