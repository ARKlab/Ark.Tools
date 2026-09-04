# AMF-09 — Two-tier metrics, guide, sample walkthrough and throughput smoke test

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-01, AMF-02, AMF-03, AMF-04, AMF-05, AMF-06, AMF-07, AMF-08
**Scope**: FRAMEWORK + OBSERVABILITY + DOCUMENTATION + SAMPLE
**Design**: [Observability](../../../messaging-throughput-prd.md#10-observability), [Testing](../../../messaging-throughput-prd.md#12-testing), [Success criteria](../../../messaging-throughput-prd.md#13-success-criteria)

## Problem

The new runtime has state that nobody can currently see: the concurrency limit,
the buffered backlog, throttling events, lock-renewal outcomes, the latency
gradient, the batch sizes actually achieved.

Emitting all of it as one flat set on the existing meter would be wrong in both
directions. Half of it is incident and autoscaling signal with a permanent
audience; half is tuning detail read once during a load test. Exported together,
every host pays for histograms it will never read, and the throttling alarm is
buried among debug counters.

## Execution map

- **Operational tier, always on**, on the existing meter
  `Ark.MediatorFramework.Messaging`: concurrency limit, in-flight, buffered,
  throttled, lock renewals by outcome. Low cardinality, alertable, and the
  autoscaling input alongside broker queue depth.
- **Advanced tier, opt-in**, on a separate meter
  `Ark.MediatorFramework.Messaging.Advanced`, keeping the plain `messaging.*` name
  prefix (the meter marks the tier, not the name): batch
  size, empty receives, backoff interval, queue wait, settle duration,
  concurrency gradient, and a `reason`-tagged decision counter.
- **Gated recording**: advanced measurements are gated on
  `MessagingProcessingOptions.AdvancedMetrics` so the cost is not paid when nobody
  is listening.
- **OTel registration**: `AddArkMessagingInstrumentation()` for the operational
  tier and `AddArkMessagingAdvancedInstrumentation()` for the advanced tier,
  following the existing `*.OTel` extension pattern. The advanced extension also
  enables the options flag, so a registered meter is never silently empty.
- **Attributes**: bounded topology only — `messaging.system`,
  `messaging.destination.name`, `ark.participant`. No message ids, correlation
  ids, exception text or other per-delivery values, on either tier.
- **Documentation and sample**: a tuning walkthrough that goes from default
  options to a measured limit using only the two tiers.
- **Throughput smoke test**: the PRD's success criteria, runnable on demand.

## Implementation steps

1. Add the operational instruments to the existing `MessagingMetrics` meter with
   stable names, units and descriptions.
2. Add the advanced meter, its instruments and the `AdvancedMetrics` gate,
   ensuring no allocation on the hot path when the gate is off.
3. Add both OTel registration extensions without introducing a new dependency.
4. Verify no forbidden high-cardinality attribute can reach either tier.
5. Write the messaging throughput guide section: options, defaults, the two
   tiers, the tuning procedure, and the compute-bound versus I/O-bound guidance.
6. Add the sample tuning walkthrough with the metric queries used to read the
   result.
7. Add the throughput smoke test (`TestCategory("integration")`, emulator, not a
   CI gate): 10 000 trivial messages at ≥ 10× the sequential baseline with zero
   lock-lost events.
8. Update the API surface baseline and any lock files touched by the series.
9. Record the release note for the pre-release breaking changes introduced by
   AMF-01 through AMF-08, including the option pair that restores single-message
   processing.

## Core code shapes

Instruments are created once per meter and are inert when no listener subscribes.
Instrumentation failures must never change settlement or transport behaviour.

The advanced gate is checked before computing a measurement, not only before
recording it, so gradient and queue-wait statistics cost nothing when disabled.

## Guide contribution

Publish the throughput and tuning guide, covering the whole series: the seam
split, the host runtime, backoff, renewal, adaptive concurrency, transport
profiles, provisioning options, and both metric tiers.

## Sample extension

Extend `Ark.MediatorFramework.Sample` with the tuning walkthrough and an opt-in
OpenTelemetry profile that registers both tiers, with example views.

## Required test coverage

- Every instrument's name, kind, unit and description is stable and asserted.
- Advanced instruments record nothing while `AdvancedMetrics` is false.
- Enabling the advanced extension enables the options flag.
- Forbidden attributes never appear on either tier.
- Operational instruments alone are sufficient to compute a scale-out decision.
- A throwing or disposed listener cannot change messaging behaviour.
- The throughput smoke test meets the success criteria on the emulator.

## Outcomes

- Reliability and autoscaling signals are always available and cheap.
- Tuning detail is available on demand without permanent cost.
- The throughput claims in the PRD are measured, not asserted.

## Acceptance

- [ ] Operational tier is implemented on the existing meter.
- [ ] Advanced tier is implemented on its own meter, gated and opt-in.
- [ ] Both OTel registration extensions exist and are documented.
- [ ] Throughput guide and sample tuning walkthrough are published.
- [ ] The throughput smoke test exists and meets the success criteria.
- [ ] API surface baseline and release notes are updated.
- [ ] The [task board](../README.md) status for AMF-09 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
