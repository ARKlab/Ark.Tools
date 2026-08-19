# AZM-05 — Transport abstraction and first-class InMemory transport

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04
**Scope**: RUNTIME + TRANSPORT
**Design**: [Transport abstraction, packaging, and InMemory transport](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport)

## Problem

The runtime must not depend on Azure SDK types, and the concrete transport is
selected at runtime while the network only declares required capabilities. A
transport contract and a shipped, fully capable InMemory transport are needed
first so every later task lands runnable and testable without Azure
infrastructure.

## Execution map

- **Transport contract**: define it in
  `Ark.Tools.MediatorFramework.Messaging` (internal-facing; not part of
  the application-visible API surface unless needed for custom transports —
  keep it public but clearly documented as an integrator seam).
- **InMemory transport**: implement in the same package as a first-class,
  shipped transport, not a test helper. Model it on the existing Rebus
  `InMemNetwork` concept (`src/common/Ark.Tools.Rebus/` prior art) but honor
  the fixed receive contract below.
- **Conformance suite**: add a reusable transport-contract test suite in
  `Ark.Tools.MediatorFramework.Tests` structured so later transports (Service
  Bus, Storage Queue) re-run the same assertions where capabilities apply.
- **Runnable state**: at task end, envelopes can be sent, scheduled,
  published, received, settled, and dead-lettered through InMemory; the full
  solution builds and tests green.
- **Stop condition**: no Azure SDK reference, no generated triggers, no
  dispatcher, no bus shim, no compression/DataBus integration in this task.

## Implementation steps

1. Define the transport contract consuming the AZM-04 envelope:
   - `MessagingCapabilities Capabilities { get; }`;
   - a hard maximum inline-envelope ceiling in bytes, plus a deterministic
     measurement seam that measures the completed native representation of an
     envelope, including headers and transport encoding. The DataBus decision
     must use this measurement rather than payload bytes alone (AZM-07);
   - `SendAsync(queueName, envelope, ctk)`;
   - `SendAsync(queueName, envelope, scheduledFor, ctk)` valid only when
     `ScheduledSend` is declared;
   - `PublishAsync(topicName, envelope, ctk)` valid only when `PubSub` is
     declared;
   - a receive seam valid only when `Receive` is declared, delivering a locked
     envelope with a native delivery count and accepting exactly one of
     `CompleteAsync`, `AbandonAsync`, or `DeadLetterAsync(reason)` per
     delivery. This PeekLock-style settlement plus delivery-count contract is
     fixed for every receive-capable transport;
   - an optional management seam (ensure queue/topic/subscription) for
     transports that support broker management.
2. Guard every capability-gated member: invoking an undeclared capability
   throws `NotSupportedException` naming the capability.
3. Implement the InMemory transport with `Capabilities = Receive | PubSub |
   ScheduledSend`:
   - named in-memory queues and topics; topic subscriptions forward a copy of
     each published envelope into each subscriber queue;
   - PeekLock semantics: a delivered message is invisible until completed,
     abandoned, or lock-expired; abandon and lock expiry increment the
     delivery count and requeue; dead-letter moves the envelope to a
     per-queue DLQ readable by tests;
   - configurable lock duration and a test-controllable clock (reuse the
     repository NodaTime clock abstractions) so lock expiry and scheduled
     delivery are testable without real waits;
   - scheduled envelopes become visible at their due time;
   - thread-safe under concurrent senders, competing consumers, and
     settlement races;
   - no hard inline-envelope ceiling (`long.MaxValue`-style unbounded): the
     network payload threshold applies alone.
4. Implement a runtime message pump for receive-capable transports: a
   start/stop async loop that takes locked deliveries and invokes a supplied
   callback. The pump is a long-running receive worker hosted only by test or
   custom hosts, never inside an Azure Functions app; AZM-13 rejects InMemory
   receive in Functions composition. AZM-09 plugs the dispatcher into this
   pump; in this task the pump
   is exercised with test callbacks only.
5. Add startup composition: registering a transport validates its
   `Capabilities` against the network `Requires` using the AZM-01 `Validate`
   contract and fails startup listing missing capabilities.
6. Write the conformance suite as capability-conditional test groups: send,
   scheduled send, publish/forwarding, settlement, delivery count, DLQ, lock
   expiry, competing consumers, and cancellation. Run it fully against
   InMemory.
7. Add XML documentation for every public member.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
transport contract, the InMemory transport as a first-class option for tests
and local development, the runtime pump, and the capability validation
failure mode.

## Sample extension

Add an InMemory transport composition to the Book sample test infrastructure
so later tasks run Book messaging scenarios without Azure resources. No
application handler changes in this task.

## Required test coverage

- Capability guards throw for undeclared operations.
- Send, scheduled visibility, publish fan-out to multiple subscriber queues.
- Complete removes; abandon requeues and increments delivery count; lock
  expiry behaves as abandon; dead-letter lands in the readable DLQ.
- Delivery count is exact across abandon/expiry cycles.
- Competing consumers never observe one lock twice concurrently.
- Scheduled delivery with a controlled clock; no arbitrary sleeps.
- Startup transport-vs-network capability validation success and failure.
- Inline-envelope boundary tests prove that headers and transport encoding are
  included in the claim-check decision.
- Conformance suite passes fully against InMemory.

## Outcomes

- Every later runtime task develops and tests against a real transport.
- InMemory cannot drift from production transports thanks to the shared
  conformance suite.

## Acceptance

- [ ] Transport contract with fixed PeekLock settlement and delivery-count
  semantics is implemented and documented.
- [ ] InMemory transport implements all capabilities and passes the full
  conformance suite.
- [ ] Capability guards and startup validation are tested.
- [ ] The [task board](../README.md) status for AZM-05 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
