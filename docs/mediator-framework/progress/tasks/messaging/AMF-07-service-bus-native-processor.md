# AMF-07 — Service Bus native processor and pull batch path

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-01, AMF-02, AMF-03, AMF-04, AMF-05
**Scope**: FRAMEWORK
**Design**: [Transport profiles](../../../messaging-throughput-prd.md#7-transport-profiles), [Rejected approaches](../../../messaging-throughput-prd.md#15-rejected-approaches)

## Problem

`ServiceBusMessagingTransport` calls `ReceiveMessageAsync` for a single message
with a fixed 1 s wait, sets no `PrefetchCount`, and has no concurrency concept.
Meanwhile `Azure.Messaging.ServiceBus` already ships `ServiceBusProcessor`, which
implements credit refill, concurrent dispatch, automatic lock renewal, error
handling and graceful stop, and is maintained by Azure. Reimplementing that means
owning AMQP link credit semantics forever — the same conclusion MassTransit
reached.

## Execution map

- **Default path**: `IMessagingNativeProcessor` over `ServiceBusProcessor` with
  `AutoCompleteMessages = false` (the framework settles explicitly),
  `ReceiveMode = PeekLock`, `MaxConcurrentCalls` ← concurrency limit,
  `PrefetchCount` ← prefetch budget, `MaxAutoLockRenewalDuration` ←
  `MaximumHandlerDuration + margin`.
- **Pull path**: `IMessagingMessageSource` over `ReceiveMessagesAsync` with real
  batching and a server-side wait window, used by the conformance suite and by
  hosts that want one unified runtime with the framework's own controller.
- **Adaptive limit**: the SDK's `MaxConcurrentCalls` is immutable per processor,
  so a limit change stops and restarts the processor, guarded by a minimum dwell
  time (default 30 s) to prevent churn.
- **Fan-out**: for very high rates, N processors over N `ServiceBusClient`
  instances (separate AMQP connections) rather than one processor with a very
  large limit.
- **Error mapping**: `ServiceBusFailureReason.ServiceBusy` → throttling signal;
  `MessageLockLost` → lock-lost settlement; both feed the controller.
- **Capabilities**: `SupportsServerSideWait = true`, `SupportsLockRenewal = true`,
  `NativeLockDuration` read from the entity, `OwnsConcurrency = true` on the
  native path.

## Implementation steps

1. Implement `IMessagingNativeProcessor` on the Service Bus transport, mapping
   `MessagingProcessingOptions` onto `ServiceBusProcessorOptions`.
2. Keep settlement explicit: `AutoCompleteMessages = false`, with the existing
   `MessagingSettlement` decisions applied unchanged.
3. Register the processor error handler and map its failure reasons onto the
   controller's signals and the operational metrics.
4. Implement the pull path with `ReceiveMessagesAsync(maxMessages, maxWaitTime)`
   and honest capability reporting.
5. Implement limit changes as stop/restart with dwell-time hysteresis, ensuring
   in-flight messages drain before the restart.
6. Read `LockDuration` from the entity and reconcile it against
   `MaximumHandlerDuration` and `MaxAutoLockRenewalDuration`, failing composition
   on an impossible combination.
7. Add optional multi-client fan-out with a documented default of one client.
8. Ensure the native path runs no framework channel, no workers and no framework
   renewer, and that its effective concurrency is reported for metrics.

## Core code shapes

The native path delegates concurrency and prefetch to the SDK but keeps
settlement, retries, DI scoping and pipeline behaviour in `MessagingDispatcher`,
so both paths share one processing semantic.

## Guide contribution

Document both paths, when to choose each, every option mapping, the restart
behaviour on limit change, the fan-out guidance, and the lock-duration
reconciliation rules.

## Sample extension

Run the sample's Service Bus profile on the native processor and document the
observed throughput against the previous sequential baseline.

## Required test coverage

- The native path settles explicitly; auto-complete never fires.
- Throttling and lock-lost failures map to the documented signals.
- A limit change restarts the processor without losing or double-settling
  in-flight messages, and respects the dwell time.
- The pull path honours `maxMessages` and `maxWaitTime`, including empty results.
- Lock-duration reconciliation fails composition for impossible combinations.
- Conformance suite passes on the emulator for both paths.
- The native path starts no framework channel, workers or renewer.

## Outcomes

- Service Bus throughput uses the SDK's maintained pump instead of one round trip
  per message.
- Both Service Bus paths share identical processing semantics.

## Acceptance

- [ ] Native processor path implemented with explicit settlement and option mapping.
- [ ] Pull batch path implemented and used by the conformance suite.
- [ ] Adaptive limit changes are applied safely with hysteresis.
- [ ] Failure reasons map to controller signals and operational metrics.
- [ ] The [task board](../README.md) status for AMF-07 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
