# AMF-05 — Adaptive concurrency with an I/O-bound guard

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-02, AMF-04
**Scope**: FRAMEWORK
**Design**: [Adaptive concurrency](../../../messaging-throughput-prd.md#62-adaptive-concurrency), [Rejected approaches](../../../messaging-throughput-prd.md#15-rejected-approaches)

## Problem

A fixed concurrency limit is either too low (the host idles) or too high (the
host overloads its dependency and turns its own excess into 429s, timeouts and
dead letters). It must adapt.

The hard part is that the local bottleneck is **not always CPU**. For a
compute-bound handler the CPU saturates and growth self-limits. For an I/O-bound
handler — SQL, HTTP, blob — the CPU stays idle at every concurrency level, so any
CPU-based or naive "keep growing while there is backlog" rule grows to
`MaxConcurrency` unconditionally. Past the dependency's saturation point,
throughput is flat while per-message latency grows linearly (Little's law), lock
renewals multiply, and the dependency starts shedding load.

## Execution map

- **Never use CPU as a control signal.** Throughput and latency only.
- **Throughput gate (AIMD)**: increase only when throughput improved by more than
  `ThroughputImprovementThreshold` (default 5 %) over the previous interval.
- **Latency-gradient guard**: track `rttNoLoad` (long-window minimum handler
  duration) and `rttShort` (short-window EWMA);
  `gradient = clamp(rttNoLoad / rttShort, 0.5, 1.0)`. Growth requires
  `gradient ≥ GradientIncreaseThreshold` (default 0.9); two consecutive intervals
  below it reduce the limit to `floor(limit × gradient)` even with no errors.
  Using a ratio cancels the workload-dependent baseline that makes raw latency
  useless as a signal.
- **Little's law cap**: `usefulConcurrency ≈ throughput × rttNoLoad`; the limit is
  hard-capped at `LittlesLawSlack × ceil(usefulConcurrency)` (default 2). Once the
  dependency saturates this cap freezes — the definitive bound on I/O-bound
  growth.
- **Multiplicative decrease** on broker throttling, lock loss/renewal failure,
  handler timeout, thread-pool starvation, and explicit backpressure.
- **Explicit signal**: `MessagingBackpressureException` thrown by a handler whose
  own downstream is the limit; the delivery is abandoned with `RetryDelay` and the
  limit halves. Recommended whenever the dependency's capacity is *known*.
- **Baseline re-arming**: `rttNoLoad` is re-armed periodically (default 10 min) so
  a permanently slower dependency does not leave a stale optimistic baseline.
- **Guard suppression**: no growth and no gradient evaluation while the buffer is
  empty; measurements there are meaningless.
- **Seam**: `IMessagingConcurrencyController` is public so the algorithm can be
  replaced without forking the host.

## Implementation steps

1. Add `IMessagingConcurrencyController` and the default AIMD + gradient
   implementation, evaluated on a fixed interval (default 5 s) with an injectable
   clock.
2. Collect per-interval throughput and handler-duration statistics without
   allocating per message.
3. Implement the gradient and the Little's-law cap, including the minimum-window
   bookkeeping and periodic re-arming.
4. Apply the decrease table, ensuring an immediate reaction to throttling rather
   than waiting for the next interval.
5. Add `MessagingBackpressureException` and wire it into the dispatcher's
   settlement decision as an abandon with `RetryDelay`, not a failure.
6. Add a thread-pool starvation probe (scheduled task queue delay) and treat it as
   a stop-growing plus decrease signal.
7. Resize the worker pool and recompute the prefetch budget when the limit
   changes, applying hysteresis so the pool does not churn.
8. Honour `AdaptiveConcurrency = false` by pinning the limit at
   `InitialConcurrency` and disabling all measurement work.

## Core code shapes

The controller reads statistics and returns a limit; it never touches deliveries,
channels or transports. That keeps it unit-testable with a scripted signal stream
and replaceable by consumers.

Worker resizing is cooperative: growing starts new workers, shrinking lets
workers exit after their current delivery settles. No worker is ever aborted.

## Guide contribution

Document the control signals, every option and default, the difference between
compute-bound and I/O-bound behaviour, when to throw
`MessagingBackpressureException`, and how to pin the limit.

## Sample extension

Add a sample handler with a simulated bounded dependency and document the limit
converging near the dependency's capacity rather than at `MaxConcurrency`.

## Required test coverage

- Increase happens only when throughput improves beyond the noise band.
- Each adverse signal produces its documented decrease.
- **I/O-bound convergence**: a simulated dependency with useful concurrency `K`
  and queueing delay beyond it stabilises the limit within
  `[K, LittlesLawSlack × K]` and never reaches `MaxConcurrency`, while CPU stays
  idle.
- The same test with `K` changing mid-run proves baseline re-arming works.
- A CPU-bound handler converges near `ProcessorCount`.
- `MessagingBackpressureException` abandons with `RetryDelay` and halves the limit.
- Thread-pool starvation prevents growth.
- Growth is blocked while the buffer is empty.
- `AdaptiveConcurrency = false` pins the limit exactly.
- The limit never leaves `[MinConcurrency, MaxConcurrency]` or the prefetch clamp.

## Outcomes

- The host converges on its real bottleneck for both work profiles.
- An I/O-bound workload cannot grow parallelism indefinitely.
- Handlers can declare backpressure the framework cannot infer.

## Acceptance

- [ ] AIMD controller with throughput gate, gradient guard and Little's-law cap is implemented.
- [ ] I/O-bound convergence is proven by a deterministic test.
- [ ] `MessagingBackpressureException` is public, documented and wired to settlement.
- [ ] Worker pool and prefetch budget resize with the limit, with hysteresis.
- [ ] `IMessagingConcurrencyController` is a replaceable public seam.
- [ ] The [task board](../README.md) status for AMF-05 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
