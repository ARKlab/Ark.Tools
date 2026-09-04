# AMF-07 — Service Bus batch receive over `ServiceBusReceiver`

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-01, AMF-02, AMF-03, AMF-04, AMF-05
**Scope**: FRAMEWORK
**Design**: [`ServiceBusProcessor` internals](../../../messaging-throughput-prd.md#43-servicebusprocessor-internals-and-what-to-build-on-instead), [Transport profiles](../../../messaging-throughput-prd.md#7-transport-profiles), [Rejected approaches](../../../messaging-throughput-prd.md#15-rejected-approaches)

## Problem

`ServiceBusMessagingTransport` calls `ReceiveMessageAsync` for a single message
with a fixed 1 s wait, sets no `PrefetchCount`, and has no concurrency concept.

`ServiceBusProcessor` is not the answer: for a non-session entity it receives
`maxMessages: 1` per call on a single link, its only amortisation is an AMQP
prefetch buffer whose locks can be neither observed nor renewed, and lowering
`UpdateConcurrency` cancels in-flight handlers. Its pump internals are `internal`,
so a subclass cannot change any of that without replacing the loop anyway. The
framework therefore owns the receive loop and builds on `ServiceBusReceiver`,
which is public, virtual and mockable, and keeps AMQP credit, link recovery and
retries below the API surface being called.

## Execution map

- **Pull source**: `IMessagingMessageSource` over
  `ServiceBusReceiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, ctk)`, which
  returns an empty list when the wait elapses — the empty signal the host needs.
- **Prefetch off**: `PrefetchCount = 0`. The framework's bounded channel is the
  buffer, and unlike the AMQP one its locks are renewable (AMF-04). This also
  sidesteps the receiver's `internal` prefetch setter.
- **Batch cap**: `MaximumBatchSize` default 100, configurable; the service imposes
  no cap, so the limit is a tuning choice bounded by the prefetch budget.
- **Server-side wait**: `maxWaitTime` carries the backoff window (AMF-03) so an
  idle queue costs one held-open request rather than a poll loop; validation
  requires a positive wait when prefetch is 0.
- **Explicit settlement and renewal**: `CompleteMessageAsync`,
  `AbandonMessageAsync`, `DeadLetterMessageAsync` and `RenewMessageLockAsync`
  called by the host; no auto-complete, no SDK renewal task.
- **Fan-out**: `ReceiveChannels > 1` opens additional receivers (separate links);
  an optional multi-client mode spreads them over N `ServiceBusClient` instances
  (separate AMQP connections) for very high rates.
- **Error mapping**: `ServiceBusFailureReason.ServiceBusy` → throttling signal;
  `MessageLockLost` → lock-lost settlement; both feed the controller (AMF-05).
- **Capabilities**: `SupportsServerSideWait = true`, `SupportsLockRenewal = true`,
  `NativeLockDuration` read from the entity.

## Implementation steps

1. Implement `IMessagingMessageSource` on the Service Bus transport with one
   receiver per receive channel, created with `ReceiveMode = PeekLock` and
   `PrefetchCount = 0`.
2. Map `maxMessages` and `maxWait` straight onto `ReceiveMessagesAsync`, clamping
   `maxMessages` to the credit the host granted and to `MaximumBatchSize`.
3. Populate `LockedUntil` and `DeliveryId` from `ServiceBusReceivedMessage`
   (`LockedUntil`, `LockToken`) so the shared renewer can drive renewal.
4. Keep settlement explicit, with the existing `MessagingSettlement` decisions
   applied unchanged, and map settle failures onto the existing outcomes.
5. Map `ServiceBusException.Reason` onto the controller's signals and the
   operational metrics.
6. Read `LockDuration` from the entity and reconcile it against
   `MaximumHandlerDuration` and the prefetch budget, failing composition on an
   impossible combination.
7. Add optional multi-client fan-out with a documented default of one client and
   one receive channel.
8. Delete the single-message `ReceiveAsync` path and its hard-coded 1 s wait.

## Core code shapes

One receiver per receive channel, no processor, no SDK-owned concurrency: the
host's worker count *is* the concurrency limit, so lowering it stops new pickups
instead of cancelling in-flight handlers.

## Guide contribution

Document the option mapping, why prefetch is 0 and the framework buffer replaces
it, the fan-out guidance (receive channels first, then clients), the
lock-duration reconciliation rules, and the reasoning against `ServiceBusProcessor`
so the choice is not silently revisited.

## Sample extension

Run the sample's Service Bus profile on the batch receive path and document the
observed throughput against the previous sequential baseline.

## Required test coverage

- `maxMessages` and `maxWaitTime` are honoured, including an empty result on an
  idle queue within the wait window.
- Settlement is explicit for complete, abandon and dead-letter, and matches the
  previous decisions message for message.
- `LockedUntil` and `DeliveryId` are populated, and renewal extends a lock past
  its original expiry.
- Throttling and lock-lost failures map to the documented controller signals.
- Lock-duration reconciliation fails composition for impossible combinations.
- Multiple receive channels do not double-deliver or double-settle.
- Conformance suite passes against the Service Bus emulator.

## Outcomes

- Service Bus receives in batches instead of one round trip per message, with
  lock-safe framework-owned buffering.
- Concurrency changes never cancel a running handler.

## Acceptance

- [ ] Pull batch source implemented over `ServiceBusReceiver` with prefetch 0 and explicit settlement.
- [ ] Server-side wait window carries the host's backoff interval.
- [ ] Receive-channel and multi-client fan-out implemented with conservative defaults.
- [ ] Failure reasons map to controller signals and operational metrics.
- [ ] The single-message receive path is removed.
- [ ] The [task board](../README.md) status for AMF-07 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
