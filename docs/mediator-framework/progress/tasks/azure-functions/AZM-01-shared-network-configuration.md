# AZM-01 — Shared messaging network configuration

**Category**: azure-functions-messaging · **Priority**: foundation
**Scope**: API + GENERATOR + HOSTING
**Design**: [Shared network/bus configuration](../../azure-functions-messaging-design.md#shared-networkbus-configuration), [Capability model and runtime transport selection](../../azure-functions-messaging-design.md#capability-model-and-runtime-transport-selection)

## Problem

Transport settings currently appear as host-local defaults. Hosts that
communicate on one network must instead share one explicit configuration for
required capabilities, serialization, compression, DataBus offload, retries,
and resource lifecycle while retaining independent identities and
subscriptions.
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
  immutable options there. Producer-only hosts (Minimal API, client apps)
  reference this package without any Functions dependency.
- **Generator model**: add symbol discovery and diagnostics under
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators`; keep Roslyn types
  internal to the generator.
- **Tests**: add API/model tests to `Ark.Tools.MediatorFramework.Tests` and
  generator fixtures beside existing Azure Functions generator tests.
- **Runnable state**: this task ships attributes, options, diagnostics, and
  the resolved descriptor only; nothing consumes them yet, and the full
  solution builds and tests green.
- **Stop condition**: do not implement envelopes, transports, triggers, or
  resource creation in this task.

## Implementation steps

1. Define the public `MessagingCapabilities` `[Flags]` enum:
   `None = 0`, `Receive = 1`, `PubSub = 2`, `ScheduledSend = 4`. XML-document
   that `Send` is implicit and not a flag.
2. Define the public network configuration attribute and immutable runtime
   options model. The class carrying the attribute supplies the network
   identity; the attribute has no separate name field. The attribute exposes
   `Requires` (a `MessagingCapabilities` value) plus serialization,
   compression, payload, DataBus offload/integrity thresholds, retry,
   lock-renewal, scheduling-limit,
   and resource-lifecycle members. Pipeline steps are deliberately not network
   settings; AZM-06 registers them per host. `IMessagingRetryPolicy`
   includes `MaximumDeliveryCount` (N), `SecondLevelRetriesEnabled`,
   `MaximumHandlerDuration`, and `RetryDelay`. Document that entity/host max
   delivery is `2N` when second-level retries are enabled and `N` otherwise,
   that `RetryDelay` maps to Storage Queue `visibilityTimeout` only, and that
   Service Bus abandon is immediate. DataBus retention is not a network
   member; concrete provider composition owns its minimum attachment lifetime.
   Validate `MaximumDeliveryCount >= 1`, and require `>= 2` when second-level
   retries are enabled so the normal handler always receives delivery 1.
3. Support one or more network profiles, with each host referencing exactly
   one profile by type.
4. Keep secrets out of attributes; allow connection/configuration key names
   and managed-identity resolution through host configuration.
5. Reject at compile time: missing or duplicate profiles and host-local
   overrides of shared settings. Reject at startup: divergent effective
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
network declaration, the capability model (declare capabilities at definition
time, select the transport at runtime), the capability/transport matrix, the
host reference, shared-settings table, and secret configuration rules.
Document that identities and subscriptions remain host-local while transport
behavior is network-shared, and that all hosts on one network share the same
runtime transport and physical resources as a documented deployment
assumption. Document that pipeline implementations are host-local because
their dependencies and environment-specific choices may differ. Document that
receive accepts every installed supported codec declared by the message
headers.

## Sample extension

Extend `samples/Ark.MediatorFramework.Sample` with one Book background network
profile declaring `Receive | PubSub | ScheduledSend`, referenced by every
Mediator Framework messaging host added by later tasks. The profile compiles
and is validated even though nothing consumes it yet.

## Required test coverage

- Network profile discovery and deterministic identity.
- Host reference to a valid profile.
- Missing, duplicate, and divergent profile diagnostics.
- Shared settings cannot be overridden by a host attribute.
- `Validate` accepts a transport capability set that is a superset of
  `Requires` and rejects any missing declared capability with a diagnostic
  naming the capability.
- Retry policy and second-level enablement validation.
- Delivery-count validation covers the second-level `N >= 2` invariant.
- The network API contains no provider-specific DataBus retention member.
- The network API contains no incoming/outgoing step registration member.

## Outcomes

- Network-wide transport behavior has one explicit source of truth.
- Networks declare required capabilities; transports are runtime decisions.
- Host identities can scale independently without configuration drift.

## Acceptance

- [ ] Network configuration API, `MessagingCapabilities`, and runtime options
  are public, documented, and covered by API-surface tests.
- [ ] Every host references exactly one validated network profile.
- [ ] Shared settings and capability declarations are enforced; no technology
  member exists.
- [ ] Book sample contains the compiling network profile.
- [ ] The [task board](../README.md) status for AZM-01 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
