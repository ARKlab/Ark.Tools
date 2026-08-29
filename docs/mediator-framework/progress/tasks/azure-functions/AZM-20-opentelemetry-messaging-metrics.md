# AZM-20 — OpenTelemetry messaging metrics

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-18, AZM-19
**Scope**: RUNTIME + OBSERVABILITY + DOCUMENTATION
**Design**: [Pipeline and context propagation](../../azure-functions-messaging-design.md#pipeline-and-context-propagation), [Test strategy and release gates](../../azure-functions-messaging-design.md#13-test-strategy-and-release-gates)

## Problem

The messaging runtime currently exposes custom processing-time and successful
queue-time histograms. Their names, units, descriptions, attributes, and outcome
semantics have not been established as a stable public telemetry contract.
Producer operations, final settlement outcomes, and delivery attempts are not
covered consistently.

Before release, the framework must define a useful low-cardinality metrics
baseline aligned with the latest stable OpenTelemetry messaging semantic
conventions. Instruments record by default through `System.Diagnostics.Metrics`;
applications opt into collection and export through OpenTelemetry.

## Execution map

- **Convention review**: use the latest stable OpenTelemetry messaging metrics
  semantic conventions available when implementation starts. Record the
  selected version in the task documentation and tests.
- **Runtime instruments**: cover send, publish, and process duration; time in
  queue; final processed-message outcome; and delivery attempt count.
- **Attributes**: permit network, participant, destination, and logical contract
  names. Exclude unbounded message-specific and exception text.
- **Settlement integration**: processing outcome is recorded after the final
  complete, abandon, or dead-letter decision rather than merely after handler
  return.
- **Default behavior**: meters and measurements exist by default; listener,
  provider, reader, and exporter registration remain opt-in.
- **Compatibility**: replace pre-release custom instruments rather than
  maintaining aliases, unless a selected stable convention requires a migration
  bridge.
- **Boundary**: payload size, compression, DataBus, retry, dead-letter totals,
  and active-operation gauges remain future improvements.

## Implementation steps

1. Audit the current stable OpenTelemetry messaging metric names, units,
   required/recommended attributes, operation names, and status values. Prefer
   standard instruments and attributes over Ark-specific equivalents.
2. Define one stable public meter name and version. Centralize instrument and
   attribute names so runtime paths cannot drift.
3. Record client-operation duration for point-to-point send, publish, and
   delayed defer after the transport accepts or rejects the operation.
4. Record process duration across the complete receive operation, including
   pipeline, handler, and final settlement.
5. Record time in queue only when a valid sent timestamp is available. Clamp
   invalid negative durations and do not infer missing timestamps.
6. Record final processed-message outcomes for completion, abandonment, and
   dead-lettering using the selected convention's status model.
7. Record native delivery attempt count from the received delivery. Do not
   synthesize or increment a header value.
8. Apply network, participant, destination, logical contract, transport system,
   operation, and outcome attributes where permitted by the selected convention.
9. Never attach exception messages, stack traces, message IDs, correlation IDs,
   attachment IDs, or other per-message values to metrics.
10. Ensure instrumentation failures cannot change messaging behavior or
    settlement. Avoid allocation when no meter listener is active.
11. Add an opt-in OpenTelemetry registration helper only if existing repository
    OTel composition cannot subscribe to the meter cleanly; do not add a new
    dependency.
12. Update the sample dashboard/query documentation and verify measurements
    through an in-process listener without requiring an external collector.

## Core code shapes

All instruments are created once from one stable `Meter`. Runtime send/publish
paths record producer duration, while the dispatcher records queue time,
processing duration, native attempt count, and outcome after settlement.

Instrumentation is always present but inert when no listener subscribes.
OpenTelemetry registration controls collection and export; messaging
composition does not force an exporter.

Metric attribute values may identify the finite deployed topology: network,
participant, destination, logical contract, and transport system. Values unique
to a delivery are forbidden.

## Guide contribution

Update the messaging observability and host-composition guides with the selected
semantic-convention version, meter name, instruments, units, attributes,
outcomes, default recording behavior, opt-in OpenTelemetry collection, and
cardinality rules.

## Sample extension

Configure the Book sample to collect the messaging meter in its opt-in
OpenTelemetry profile. Document example views for producer latency, processing
latency, queue delay, settlement outcomes, and delivery attempts without
requiring a specific exporter.

## Required test coverage

- Meter name/version and every instrument name, type, unit, and description are
  stable.
- Send, publish, and defer record success and failure durations.
- Processing duration includes final settlement.
- Queue time records only for valid sent timestamps and never emits negative
  values.
- Complete, abandon, and dead-letter outcomes are distinguishable.
- Delivery attempts use the native delivery count.
- Network, participant, destination, contract, transport, operation, and outcome
  attributes follow the selected convention.
- Message IDs, correlation IDs, exception text, and attachment IDs never appear
  as attributes.
- No listener produces minimal/no per-operation instrumentation allocation.
- A throwing or disposed listener cannot change transport or settlement
  behavior.
- InMemory, Storage Queue, Service Bus, and outbox producer paths emit consistent
  measurements.

## Outcomes

- Messaging metrics have a stable pre-release contract.
- Operators can measure producer latency, queue delay, processing, outcomes, and
  delivery attempts across transports.
- Collection remains standards-based and opt-in without changing runtime
  behavior.

## Acceptance

- [ ] The selected stable OpenTelemetry messaging metric convention is recorded.
- [ ] Producer and consumer baseline instruments are implemented with stable
  names, units, descriptions, and attributes.
- [ ] Processing outcomes reflect final settlement.
- [ ] Default instrumentation and opt-in collection/export are documented and
  tested.
- [ ] Forbidden high-cardinality attributes are absent.
- [ ] Sample and observability guides are updated.
- [ ] The [task board](../README.md) status for AZM-20 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
