# AZF-07 — Outbound-only Rebus composition for Functions

**Category**: azure-functions · **Priority**: sample-parity · **Scope**: FRAMEWORK TEST SUPPORT + SAMPLE

## Problem

An Azure Function app may send mediator messages but must not host the sample's
long-running Rebus processor. The sample needs a real one-way transport and
generated routing without accidentally registering receivers or starting workers.

## Prerequisites

- AZF-05 merged.
- AZD-08 decided.
- Review `SampleComposition`, `SampleBusHostedService`,
  `ApiHost.WithRebus(Queue.OneWay)`,
  `DrainableInMemTransportExtensions` and generated Rebus routing tests.

## Implementation steps

1. Place the approved drainable in-memory one-way transport support in the narrowest
   reusable test-only location without changing its existing behavior. If the
   Functions scenario needs incompatible semantics, add a separately named
   `DrainableV2`. Do not expose production public API solely for the sample if an
   internal test project reference is sufficient.
2. Add a Function-host composition method that configures Rebus routing through
   `ConfigureArkRebusRouting<TAssemblyMarker>()` but never calls
   `RegisterArkRebusHandlersFromAssembly`.
3. Azure configuration uses `UseAzureServiceBusAsOneWayClient`, preferring
   `DefaultAzureCredential` and configuration binding consistent with repository
   conventions. A connection string, when supported, comes only from external
   configuration.
4. Local tests use the drainable in-memory one-way transport and inspect messages
   received by a separately created consumer/test receiver.
5. Do not configure an input queue, worker count, subscriptions, error queue,
   receive decorators or inbound retry pipeline in the Function app.
6. Ensure Function shutdown disposes the one-way bus cleanly and invocation
   cancellation does not leave half-written sends.
7. Demonstrate an HTTP request whose existing handler sends a typed owned message,
   then returns its normal accepted/immediate response. The separate receiver
   asserts the generated owner route and payload.
8. Document that request/reply and local receive semantics are unsupported in this
   host; consumers run in a worker process.

## Caveats

- Azure Service Bus has no Azurite emulator; do not pretend an unrelated emulator
  validates transport behavior.
- A mocked `IBus` does not satisfy the routing demonstration.
- Preserve existing drainable behavior and tests; do not introduce a breaking
  semantic change for the Functions host.
- Do not start the sample SQL outbox processor in the Function host. If atomic
  outbox sending is required later, it needs a separate lifecycle design.
- Never commit Azure Service Bus connection strings or credentials.
- Outbound `IBus.SendLocal` may be invalid for a one-way client; use owner routing
  and the correct send API, with a regression test.

## Required test coverage

- Composition resolves `IBus`, sends through one-way transport and has zero
  registered generated receive handlers.
- Generated owner routing sends to the expected queue.
- Separate receiver observes payload and propagated allowed headers.
- Host disposal completes and no receive worker starts.
- Configuration tests cover managed identity and missing configuration without
  contacting Azure.

## Outcomes

- The Function sample demonstrates production-shaped outbound Rebus and a real
  local one-way test without hosting a processor.

## Acceptance

- [x] AZD-08 is recorded as decided.
- [ ] Function composition has no input queue or receive handlers.
- [ ] Routing/payload are verified by a separate receiver, not an `IBus` mock.
- [ ] Existing drainable behavior remains backward compatible.
- [ ] Managed identity is the documented Azure default; no secret is committed.
- [ ] Lifecycle and unsupported request/reply behavior are documented.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
