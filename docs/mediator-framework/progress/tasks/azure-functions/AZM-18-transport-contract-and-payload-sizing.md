# AZM-18 — Transport contract and payload sizing

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-17
**Scope**: PUBLIC API + RUNTIME + TRANSPORTS
**Design**: [Transport abstraction](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport), [DataBus claim-check](../../azure-functions-messaging-design.md#11-databus-claim-check)

## Problem

The current transport contract mixes a network payload policy with native
envelope measurement, treats point-to-point receive as if it were independent
from send. These choices leak transport limits into network declarations.

Before release, transports must own their physical payload limit and header
measurement, and point-to-point messaging must be one explicit capability.

## Execution map

- **Public capability API**: rename `MessagingCapabilities.SendReceive` to
  `SendReceive`; a network without it is publish-only.
- **Public bus API**: delayed `Send` overloads are renamed to `Defer`; current-message
  deferral remains future work and is not implemented here.
- **Network declarations/options**: remove maximum transport payload and DataBus
  offload threshold members and defaults.
- **Transport contract**: replace `MeasureNative` with a static per-transport
  complete payload limit and native header-size computation.
- **Payload runtime**: calculate headers plus body, compress before sizing, and
  transparently claim-check through DataBus when the selected transport limit is
  exceeded.
- **Conformance**: update every transport, outbox path, generated descriptor,
  sample host, and transport conformance suite together.

## Implementation steps

1. Rename the capability and generated API-surface value from `Receive` to
   `SendReceive`. Reject point-to-point `Send` unless the network declares it;
   allow a `PubSub`-only network to publish.
2. Rename both delayed `IBus.Send` overloads and their implementations to
   `Defer`. Keep immediate `Send` unchanged and do not add a current-delivery
   deferral API.
3. Remove `MaximumTransportPayloadBytes`,
   `DataBusOffloadThresholdBytes`, their defaults, generated values, validation,
   documentation, and configuration.
4. Define the transport's maximum complete payload size as a static interface
   contract. Runtime composition must retain that fixed value without reflection
   or provider type switches.
5. Keep native-header measurement for reference envelopes and add complete
   native-payload measurement. Document whether each result is exact or a
   conservative upper bound; Storage Queue must measure its single Base64
   encoded canonical envelope, not raw header plus body lengths.
6. Define complete payload size through the transport-native payload method.
   Serialization remains streaming/generic-only; compression completes before
   final body sizing.
7. When the complete inline payload exceeds the transport maximum, store the body
   in DataBus, replace it with attachment-reference headers, recompute native
   header size, and fail fast if the reference-only payload still exceeds the
   transport maximum.
8. Apply identical final sizing before direct sends, publishes, scheduled sends,
   and native outbox persistence. The outbox processor sends an already validated
   representation without rerunning claim-check logic.
9. Regenerate API baselines and inspect affected emitted `.g.cs` output.

## Core code shapes

Each concrete transport type supplies a fixed maximum complete payload size
through the static transport contract and computes native size for a complete
header/payload pair. Native header sizing and logical-name mapping are static
abstract members of the generic transport contract; the non-generic transport
seam remains available for DI. The default implementation adds body length;
encodings such as Storage Queue override it to measure the exact native
representation.

Storage Queue advertises a conservative 48 KiB effective ceiling (three
quarters of the native 64 KiB limit) to account for envelope framing overhead
after Base64 encoding.
The shared runtime owns the transparent compression/DataBus decision.

`SendReceive` gates both routing to a processing participant and receive
hosting. `PubSub` remains independent. Scheduled delivery remains separately
gated, with delayed point-to-point delivery exposed as `Defer`.

## Guide contribution

Update the transport matrix, bus API, DataBus, outbox, retry, resource
lifecycle, and Azure Functions guides. Explain complete payload sizing,
transparent offload, and publish-only networks.

## Sample extension

Update the Book sample to demonstrate a publish-only participant, transparent
DataBus offload under different transport limits, and the renamed delayed-send
API.

## Required test coverage

- Capability numeric values and generated strings use `SendReceive`.
- Point-to-point sends fail without `SendReceive`; publish works with `PubSub`
  alone.
- Immediate `Send` and delayed `Defer` route and schedule correctly.
- Network declarations and generated options expose no transport payload or
  DataBus offload threshold.
- Every transport reports its fixed complete payload limit and header size
  consistently.
- Boundary tests cover body exactly below, at, and above each transport limit,
  including header growth and attachment-reference recomputation.
- Compression occurs before sizing and DataBus offload.
- Direct send, publish, defer, and outbox enqueue apply the same validation.
- InMemory, Storage Queue, and Service Bus conformance suites remain green.

## Outcomes

- Transport limits and header encoding are owned by transports.
- Compression and DataBus offload are transparent to network declarations.
- Point-to-point messaging has one accurate capability.

## Acceptance

- [x] `SendReceive` replaces `Receive` throughout public, generated, and
  documented surfaces.
- [x] Delayed sends use `Defer`; current-message deferral remains out of scope.
- [x] Network payload/offload settings are removed.
- [x] Every transport implements the static payload limit and header sizing
  contract.
- [x] Runtime claim-check uses complete headers-plus-body size.
- [x] Sample, guides, API baselines, and generated-source inspections are
  updated.
- [x] The [task board](../README.md) status for AZM-18 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
