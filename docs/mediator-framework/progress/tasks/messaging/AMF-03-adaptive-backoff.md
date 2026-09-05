# AMF-03 — Idle, error and no-capacity backoff

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-02
**Scope**: FRAMEWORK
**Design**: [Idle backoff](../../../messaging-throughput-prd.md#64-idle-backoff), [Provider facts](../../../messaging-throughput-prd.md#44-provider-facts-that-constrain-the-design)

## Problem

The Storage Queue transport hard-codes `Task.Delay(250 ms)` inside the transport
whenever a receive comes back empty. That is 4 billed requests per second for an
idle queue, forever, on every deployed host, and it is invisible and
unconfigurable because it lives below the seam. Service Bus has the opposite
problem: a fixed 1 s wait window with no way to lengthen it when the queue is
quiet.

An empty queue must cost asymptotically nothing without adding latency when work
arrives.

## Execution map

- **Three distinct waits**, not one: empty result, no credit, transport error.
  They have different causes and must not share state.
- **Empty**: exponential with full jitter from `MinPollInterval` (50 ms) to
  `MaxPollInterval` (5 s), doubling per consecutive empty result, reset to the
  minimum on the first non-empty batch **for that receive loop**.
- **No credit**: no timer at all — await channel capacity. Polling while the host
  cannot accept work is pure waste, and holding a slot during backoff (as Rebus
  does) is the mistake being avoided.
- **Transport error**: fixed jittered cooldown (`ErrorCooldown`, default 10 s),
  independent of the empty-backoff state, with structured logging that names the
  queue and the failure.
- **Server-side wait**: when `SupportsServerSideWait = true`, backoff grows the
  `maxWait` passed to `ReceiveBatchAsync` instead of sleeping, so latency stays low
  while the call rate falls.
- **Transport cleanup**: the hard-coded delay is removed from the Storage Queue
  transport; the transport waits only as instructed by `maxWait`.

## Implementation steps

1. Add a backoff component owned by the receive loop, with an injectable clock
   and an injectable jitter source so tests are deterministic.
2. Implement full jitter (`delay = random(0, min(max, base × 2^n))`) to avoid
   synchronised polling across replicas of the same host.
3. Wire the three situations to their own paths in the receive loop, ensuring an
   error does not reset the empty counter and vice versa.
4. Grow `maxWait` up to `MaxPollInterval` for server-side-wait transports and
   assert the transport honours it.
5. Delete the hard-coded `Task.Delay` from `StorageQueueMessagingTransport`.
6. Validate `MinPollInterval ≤ MaxPollInterval` and both against the visibility
   window at composition time.
7. Record the advanced-tier backoff instruments defined in AMF-09 behind the same
   options flag.

## Core code shapes

Backoff state is per receive loop, not per host, so that adding
`ReceiveChannels > 1` does not couple loops that observe different broker
behaviour.

## Guide contribution

Document the three waits, the defaults, the jitter, the server-side-wait
behaviour, and the idle cost figures for both Azure transports.

## Sample extension

Show the idle-cost effect in the sample readme: request rate for an idle queue
before and after, and the option names to tune it.

## Required test coverage

- Empty results double the wait up to the maximum and no further.
- A non-empty batch resets the wait to the minimum.
- Jitter keeps every wait within `[0, cap]` and is not constant.
- A transport error waits `ErrorCooldown` and does not disturb the empty counter.
- No-credit waits on channel capacity and issues no receive call.
- Server-side-wait transports receive a growing `maxWait`, not a client sleep.
- The Storage Queue transport contains no internal delay.
- Latency for the first message after an idle period stays within one
  `MaxPollInterval`.

## Outcomes

- Idle Storage Queues drop from ~4 req/s to ≤ 0.2 req/s.
- Idle Service Bus receivers hold one long request instead of polling.
- Backoff is configurable, observable and testable.

## Acceptance

- [x] Empty, no-credit and error waits are implemented independently.
- [x] Exponential backoff with full jitter and per-loop reset is tested deterministically.
- [x] Server-side wait growth is used where the transport supports it.
- [x] The hard-coded transport delay is removed.
- [x] The [task board](../README.md) status for AMF-03 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
