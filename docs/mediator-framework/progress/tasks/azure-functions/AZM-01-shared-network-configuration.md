# AZM-01 — Shared messaging network configuration

**Category**: azure-functions-messaging · **Priority**: foundation
**Scope**: API + GENERATOR + HOSTING
**Design**: [Shared network/bus configuration](../../azure-functions-messaging-design.md#shared-networkbus-configuration), [Capability model and runtime transport selection](../../azure-functions-messaging-design.md#capability-model-and-runtime-transport-selection)

## Problem

Transport settings currently appear as host-local defaults. Participants that
communicate on one network must instead share one explicit declaration: the
member list, the required capabilities, and the payload/DataBus thresholds,
resource lifecycle. Host-specific connection key names remain host metadata.
Serialization, compression, and
retry are participant-owned (AZM-02), not network settings.
The network must not name a technology: the concrete transport is a runtime
composition decision, and the network declares only the capabilities it
requires.

## Execution map

- **Public API**: add network attributes/options/enums under
  `src/mediator-framework/Ark.Tools.MediatorFramework`, including the
  `MessagingCapabilities` flags enum with `Receive`, `PubSub`, and
  `ScheduledSend` (plain `Send` is implicit and always available; it is not a
  capability and never appears in capability tables or flags).
- **Runtime project**: create the transport-neutral
  `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging` project
  following existing project conventions; resolve the network descriptor and
  immutable options there. Sender-only and publisher participants (Minimal
  API, client apps)
  reference this package without any Functions dependency.
- **Generator model**: add symbol discovery and diagnostics under
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators`; keep Roslyn types
  internal to the generator.
- **Tests**: add API/model tests to `Ark.Tools.MediatorFramework.Tests` and
  generator fixtures beside existing Azure Functions generator tests.
- **Runnable state**: this task ships the network attribute, options, and the
  resolved descriptor only. `Members` is retained as an opaque type list until
  AZM-02 introduces participant declarations and validates membership. Nothing
  consumes the descriptor yet, and the full solution builds and tests green.
- **Stop condition**: do not implement envelopes, transports, triggers, or
  resource creation in this task.

## Implementation steps

1. Define the public `MessagingCapabilities` `[Flags]` enum:
   `None = 0`, `SendReceive = 1`, `PubSub = 2`, `ScheduledSend = 4`. XML-document
   that `Send` is implicit and not a flag.
2. Define the public network configuration attribute and immutable runtime
   options model. The class carrying the attribute supplies the network
   identity; the attribute has no separate name field. The attribute exposes
   `Members` (the participant types belonging to the network), `Requires`
   (a `MessagingCapabilities` value), maximum transport payload and
   decompressed-payload thresholds, DataBus offload/integrity thresholds,
   scheduling-limit, and resource-lifecycle members.
   The network carries no serialization, compression, retry, or pipeline
   members: serialization and compression are participant-owned (receive is
   header-driven), retry is participant-owned (per-queue), and AZM-06
   registers pipeline steps per participant on its host binding.
   DataBus retention is not a network
   member; concrete provider composition owns its minimum attachment lifetime.
   Default the maximum transport payload threshold to 240 000 bytes (safe for
   Service Bus standard tier, leaving header headroom); document that AZM-07
   also measures the complete native envelope through the AZM-05 transport
   seam, and that a network intended for Storage Queue should set the
   threshold at or below 46 080 bytes.
3. Define `Members` as the sole membership input, but do not validate its
   contents in this task: `[MessagingParticipant]` does not exist until
   AZM-02. AZM-02 validates that every member is a participant declaration,
   rejects duplicate member entries and membership in multiple networks, and
   derives the validated membership model.
4. Keep secrets out of attributes; allow connection/configuration key names
   and managed-identity resolution through host configuration.
5. Reject network-level declarations of participant-owned settings
   (serialization, compression, retry). Defer all `Members` validation to
   AZM-02. Reject at startup: divergent effective
   options and a composed transport that does not support every declared
   capability (the transport seam arrives in AZM-05; specify the startup
   validation contract now as an options-model method
   `Validate(MessagingCapabilities transportCapabilities)`).
6. Do not add any technology member. Any `AzureServiceBus`/`AzureStorageQueue`
   enum or property on the network is wrong by design.
7. Include the resolved network identity in generated manifests, diagnostics,
   resource ownership calculations, and the `amf1-network` wire header
   written on send and verified on receive (implemented by AZM-04).
8. Add XML documentation and API-surface entries for every public member.
9. Define startup validation inputs for a concrete DataBus provider's declared
    minimum attachment lifetime. The network does not own retention; AZM-07
    validates the provider declaration against bounded scheduling/retry values.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
network declaration, the member-list model (participants inherit the ability
to send, receive, publish, and subscribe from membership), the capability
model (declare capabilities at definition
time, select the transport at runtime), the capability/transport matrix, the
member reference, shared-settings table, and secret configuration rules.
Document that serialization, compression, and retry are participant-owned
while payload/DataBus thresholds and lifecycle are network-shared, and that
all participants on one network
share the same
runtime transport and physical resources as a documented deployment
assumption. Document that pipeline implementations are participant-local
because
their dependencies and environment-specific choices may differ. Document that
receive accepts every installed supported codec declared by the message
headers.

## Sample extension

Defer the Book network declaration to AZM-02, which introduces the participant
types it lists. This task must not create placeholder participant metadata or
special-case unvalidated sample members.

## Required test coverage

- Network discovery and deterministic identity.
- Participant-owned settings (serialization, compression, retry) cannot be
  declared on the network.
- `Validate` accepts a transport capability set that is a superset of
  `Requires` and rejects any missing declared capability with a diagnostic
  naming the capability.
- The network API contains no provider-specific DataBus retention member.
- The network API contains no serialization, compression, retry, or
  incoming/outgoing step registration member.

## Outcomes

- Network-wide transport behavior has one explicit source of truth.
- Networks declare the member-list input and required capabilities; validated
  membership is established by AZM-02, and transports are runtime decisions.
- Participant identities can scale independently without configuration drift.

## Acceptance

- [x] Network configuration API, `MessagingCapabilities`, and runtime options
  are public, documented, and covered by API-surface tests.
- [x] `Members` is available as an opaque network declaration input; AZM-02
  owns participant and membership validation.
- [x] Shared settings and capability declarations are enforced; no technology
  member exists.
- [x] No sample network declaration depends on participant metadata that is
  introduced by a later task.
- [x] The [task board](../README.md) status for AZM-01 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
