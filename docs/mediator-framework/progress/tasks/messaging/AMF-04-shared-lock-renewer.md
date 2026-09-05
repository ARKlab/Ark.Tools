# AMF-04 — Shared lock renewer driven by native lock duration

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-02
**Scope**: FRAMEWORK
**Design**: [Lock renewal](../../../messaging-throughput-prd.md#65-lock-renewal), [Prefetch budget](../../../messaging-throughput-prd.md#63-prefetch-budget)

## Problem

`MessagingDispatcher` renews each in-flight delivery from its own
`Task.Delay(15 s)` loop. That is one timer per message, a constant that ignores
the entity's configured `LockDuration`, and it starts only when the handler
starts — so a prefetched delivery's lock can expire while it sits in the buffer.
Prefetch is unsafe until renewal moves out and starts at fetch time.

Rebus solves this by **disabling** renewal when prefetch is enabled. That trades
lock loss for guaranteed duplicate processing and is explicitly rejected.

## Execution map

- **One renewer per host**: a single timer scanning in-flight and buffered
  deliveries, batching due renewals with `Task.WhenAll`. O(1) timers instead of
  O(in-flight).
- **Renew at 50 % of remaining**: due when
  `now ≥ lockedUntil − max(RenewalSafetyMargin, (lockedUntil − acquiredAt) / 2)`,
  driven by the transport-reported `LockedUntil`, never by a constant.
- **Starts at buffer entry**: a delivery is registered with the renewer when it
  enters the buffer, not when a worker picks it up. This is what makes prefetch
  safe.
- **Failure handling**: a renewal failure cancels that delivery's handler token,
  removes it from the renewer, records the operational lock-renewal instrument
  with `outcome=failure`, and feeds the concurrency controller (AMF-05).
- **Non-renewable transports**: `SupportsLockRenewal = false` gets composition-time
  validation instead — `MaximumHandlerDuration + expected buffer wait` must fit
  the visibility window or startup fails.
- **Dispatcher cleanup**: the per-delivery renewal loop and the
  `lockRenewalInterval` constructor parameter are removed.

## Implementation steps

1. Add `MessagingLockRenewer` as a host-scoped component with an injectable clock.
2. Register deliveries on buffer entry, deregister on settlement, and make both
   operations safe against concurrent worker activity.
3. Compute the due time from `LockedUntil` and `RenewalSafetyMargin`, refreshing
   `LockedUntil` from the transport's renew result.
4. Batch due renewals per tick and bound the batch so a large in-flight set cannot
   stall the timer.
5. Cancel the handler token on renewal failure and ensure the dispatcher treats it
   as a lock-lost outcome with the existing settlement rules.
6. Remove the dispatcher's renewal loop and its constructor parameter, updating
   every call site.
7. Add composition-time validation for non-renewable transports.
8. Ensure renewal never runs after settlement has begun for the same delivery.

## Core code shapes

The renewer owns the authoritative `LockedUntil` per delivery. Nothing else
mutates it, which removes the class of races that appear once renewal and
settlement run on different tasks.

## Guide contribution

Document the renewal rule, its interaction with prefetch, the failure behaviour,
and the validation applied to transports that cannot renew.

## Sample extension

Add a sample handler slow enough to require at least two renewals, so the sample
exercises the path end to end.

## Required test coverage

- Renewal fires at ~50 % of remaining lock, with a fake clock.
- A delivery buffered but not started is still renewed.
- Renewal failure cancels the handler token and records the failure outcome.
- Settlement after renewal succeeds with the refreshed lock state.
- Renewal never runs concurrently with settlement of the same delivery.
- Non-renewable transports fail composition when the durations cannot fit.
- Timer count is independent of in-flight count.

## Outcomes

- Prefetch and lock renewal coexist safely.
- Renewal cadence follows the entity, not a hard-coded constant.
- Renewal cost no longer scales with in-flight messages.

## Acceptance

- [x] A single host-scoped renewer replaces the per-delivery loop.
- [x] Renewal starts at buffer entry and follows transport-reported lock state.
- [x] Renewal failure cancels the handler and is recorded operationally.
- [x] Non-renewable transports are validated at composition time.
- [x] The [task board](../README.md) status for AMF-04 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
