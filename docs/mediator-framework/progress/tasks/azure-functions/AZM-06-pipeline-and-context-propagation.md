# AZM-06 — Incoming/outgoing pipeline and context propagation

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04, AZM-05
**Scope**: RUNTIME + HOSTING
**Design**: [Pipeline and propagation](../../azure-functions-messaging-design.md#10-pipeline-and-propagation)

## Problem

Generated triggers must support cross-cutting transport behavior without
embedding user-context or OpenTelemetry logic in every generated method.
Rebus provides this through `IPipeline` and direction-specific steps.

## Execution map

- **Prior art**: read `src/common/Ark.Tools.Rebus/ApplicationInsightsStep.cs`,
  `UserFlowStep.cs`, and `Ex.cs` before defining the new contracts.
- **Public API/runtime**: put transport-neutral step/context contracts in
  `Ark.Tools.MediatorFramework`; put transport context adapters and built-in
  steps in the `Ark.MediatorFramework.Messaging` namespace of `Ark.Tools.MediatorFramework`.
- **Ordering**: represent stages with framework-owned stable identifiers and
  validate missing anchors, duplicate registrations, and ordering cycles at
  startup.
- **Lifetime**: resolve step instances through SimpleInjector per invocation
  unless explicitly registered singleton; never cache scoped state.
- **Stop condition**: do not copy Rebus interfaces or expose Azure SDK objects
  in public step contracts.

## Implementation steps

1. Define transport-neutral incoming and outgoing step contracts with
   continuation-based async processing.
2. Define named relative positions around deserialize, dispatch, serialize,
   send, and settlement.
3. Provide registration for custom steps on the participant's host binding
   (the assembly-level host attribute, AZM-10/AZM-13), with deterministic
   ordering.
   Participants referencing one network may intentionally use different steps
   because
   implementations can add heavy dependencies and environment-specific
   behavior. Steps are host-local: they live on the host binding, not on the
   shared participant declaration. The network owns only stable stage
   identifiers and contracts.
4. Implement the existing `ark-user-*` propagation behavior as an opt-in
   built-in step.
5. Implement an opt-in OpenTelemetry step that propagates W3C
   `traceparent`, `tracestate`, and `baggage` and creates/continues an
   activity around message processing.
6. Ensure outgoing steps can add headers before serialization and incoming
   steps can restore context before handler resolution.
7. Reject custom attempts to override reserved routing, content, encoding,
   attachment, and identity headers.
8. Ensure exceptions and cancellation pass through the pipeline unchanged.
   AZM-09 owns handler execution and all completion, abandon, and
   dead-letter settlement decisions.
9. Keep the step contracts independent of Azure Service Bus, Storage Queue,
   and Rebus types.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
incoming/outgoing step registration, relative ordering, user-context
propagation, OpenTelemetry propagation, and reserved-header protection.

## Sample extension

Register the opt-in user-context and OpenTelemetry steps on the applicable Book
sample host bindings/composition. Pipeline behavior is proven in
framework
tests over the InMemory transport in this task; end-to-end Book assertions
through dispatch land with AZM-09.

## Required test coverage

- Deterministic relative ordering around every named stage.
- User context outgoing header creation and incoming principal restoration.
- OpenTelemetry parent/context propagation and activity lifecycle.
- Custom step adds an allowed header.
- Reserved-header override is rejected.
- Step failure and cancellation are observable by the caller/test callback and
  do not corrupt pipeline ordering or context. Handler-failure settlement is
  covered by AZM-09.
- Multiple concurrent invocations do not share step state or context.
- Two participants referencing one network may resolve different step sets
  while each
  participant's ordering remains deterministic.

## Outcomes

- User context and OTel propagation are reusable, opt-in transport steps.
- Future transport concerns can be added without changing generated triggers.
- Send and Publish share the same outgoing pipeline.

## Acceptance

- [ ] Incoming/outgoing step APIs are public, documented, and transport-neutral.
- [ ] Named ordering is deterministic and tested.
- [ ] User-context and OpenTelemetry steps are opt-in and tested.
- [ ] Additional header injection and reserved-header protection are tested.
- [ ] Pipeline failures and cancellation are explicit and reach the dispatcher
  seam unchanged; AZM-09 verifies their settlement behavior.
- [ ] The [task board](../README.md) status for AZM-06 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
