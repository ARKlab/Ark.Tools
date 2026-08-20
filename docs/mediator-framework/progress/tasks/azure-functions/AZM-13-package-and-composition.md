# AZM-13 — Functions messaging package and composition

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-05, AZM-06, AZM-07, AZM-07A, AZM-08, AZM-09, AZM-10, AZM-11, AZM-12
**Scope**: PACKAGE + HOSTING
**Design**: [Packaging](../../azure-functions-messaging-design.md#packaging), [Transport abstraction](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport)

## Problem

The runtime and generator must be consumable as a package and compose with the
existing Functions HTTP host without starting a Rebus worker or duplicating
SimpleInjector registrations.

## Execution map

- **Projects**: finalize the package split: the transport-neutral runtime
  (network options, message context, codecs, pipeline contracts, DataBus,
  transports, bus, dispatcher, lifecycle) lives in the
  `Ark.MediatorFramework.Messaging` namespace of the
  `Ark.Tools.MediatorFramework` assembly (a `Messaging/` sub-folder), and
  `Ark.Tools.MediatorFramework.AzureFunctions`
  (trigger generation and Functions hosting adapters) depends on it, plus
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators` and the generic
  `Ark.Tools.MediatorFramework.Messaging.Generators` (network validation
  and the participant-owned contract mappers), central package
  versions, and lock files.
- **Producer composition**: expose a sender/publisher registration usable from
  any process (Minimal API, console client, Functions) that composes only the
  participant type reference, network options, transport, host-local outgoing
  pipeline, DataBus, and the
  restricted `IBus`; the sender identity is always the participant identity
  (explicit or normalized class-name default), and topic `Ensure` covers the
  events in the participant's `Publishes`; it must not
  pull Functions dependencies or register dispatch, triggers, queues, or
  subscriptions.
- **Composition**: expose one startup extension that accepts/resolves the
  generated network/participant descriptor, selects the runtime transport
  (`UseInMemory`/`UseAzureServiceBus`/`UseAzureStorageQueue`-style), validates
  transport capabilities against the network declaration, and registers the
  native bus, codecs, pipeline, DataBus, dispatcher, settlement, and lifecycle
  services.
  The generated descriptor owns the static generic serde/dispatch entries:
  `typeof(T)` maps to the current wire name for writes, while a wire name maps
  to a closed generic deserializer and processor dispatch for reads. JSON
  codecs consume the host's source-generated `JsonSerializerOptions`; contract
  descriptors and protocol-neutral codec APIs expose no JSON metadata. The
  generated shape mirrors Minimal API and HttpTrigger binding, response
  serialization, and handler dispatch.
- **DI**: follow the existing Azure Functions HTTP/SimpleInjector composition;
  do not create a second container or duplicate application registrations.
- **Mode selection**: fail startup if both Rebus and Mediator Framework buses
  are registered for one logical topology.
- **Package gate**: verify analyzer placement, runtime assets, trimming, API
  surface, and generated source in a consuming fixture.

## Implementation steps

1. Add or extend the Azure Functions runtime package and analyzer packaging
   following the existing HTTP package shape.
2. Reference only the approved Azure Functions Worker and Service Bus/Storage
   Queue dependencies, centralizing versions and lock files.
3. Add startup extensions for the participant type reference, runtime
   transport selection with capability validation, resource lifecycle,
   serializers, the participant's retry and compression settings, scoped
   dispatch, restricted bus registration,
   host-binding pipeline steps, and shared DataBus configuration. Include
   the
   sender/publisher registration path for non-Functions processes. Startup
   must
   also fail when the composed runtime transport does not match the trigger
   binding recorded in the generated manifest of a receive-capable Functions
   host, naming both; sender-only/publisher participants and InMemory-composed
   participants are exempt,
   because the generator-side assembly attribute and the runtime transport
   selection are different files that can drift. Functions composition rejects
   the InMemory receive transport outright: the InMemory pump is a
   long-running receive worker, so InMemory consumer participants are hosted
   in test or custom hosts, never in a Functions app. Functions composition
   accepts exactly one bound messaging participant per Function App; it
   rejects multiple generated host bindings or a composed descriptor that
   differs from the single generated binding. A Storage Queue messaging app
   must not compose an unrelated QueueTrigger that needs conflicting
   host-wide `queues` settings.
4. Provide explicit, mutually exclusive composition paths for Rebus versus a
   Mediator Framework messaging network. Do not register both bus
   implementations for one logical topology.
5. Ensure no Rebus worker, Rebus outbox processor, or native SQL outbox
   processor starts in Functions composition. Generated Functions triggers are
   the intended receivers. Functions may register the AZM-14A native outbox
   producer/enlistment seam, but polling is hosted elsewhere.
6. Add configuration validation for missing connections, credentials, entity
   settings, and conflicting participant
   references.
   Participant identities are compile-time validated (AZM-02), including
   reserved names.
7. Add package-content and trim/analyzer checks.

## Guide contribution

Update [`guide/host-setup-and-composition.md`](../../../guide/host-setup-and-composition.md)
and [`guide/azure-functions.md`](../../../guide/azure-functions.md) with package
registration, shared network resolution, participant-local pipeline
registration, and
the prohibition on starting Rebus workers or outbox processors in Functions.

## Sample extension

Add the Book sample's Azure Functions messaging composition beside the existing
Rebus processor composition. Both must reuse application registration and
handlers while selecting their own receiver host.

## Required test coverage

- Package contains the generator under `analyzers/dotnet/cs`.
- Runtime resolves the restricted bus and dispatch services.
- Existing HTTP generated Functions remain unaffected.
- Missing configuration fails with explicit diagnostics.
- Transport capability mismatch against the network declaration fails startup
  naming the missing capability.
- A composed runtime transport that does not match the generated manifest's
  trigger binding fails startup naming both.
- A sender/publisher composition resolves a working `IBus` in a plain host
  (no Functions references) and cannot resolve dispatch or trigger services.
- Functions composition of the InMemory receive transport fails startup with
  an explicit diagnostic.
- Managed identity and external connection configuration are supported without
  secrets in source.
- Rebus and Mediator Framework bus compositions are independent and mutually
  exclusive per logical topology.
- Pipeline registration is host-local (on the host binding) and
  deterministic; DataBus composition
  remains shared by the network.
- Functions composition rejects multiple messaging participant bindings with
  an explicit diagnostic before registering dispatch or triggers.
- Azure Blob DataBus composition resolves with data-plane credentials and does
  not require lifecycle-policy management permissions.

## Outcomes

- A consuming Functions app can opt into messaging for one bound participant
  with unambiguous host-wide queue settings.
- HTTP and messaging generation coexist deterministically.
- Functions remains scale-to-zero friendly and does not become a worker host.

## Acceptance

- [ ] Package and analyzer assets are correct and locked.
- [ ] Startup composition is documented and validates configuration.
- [ ] No Rebus receiver or outbox processor starts in the Functions process.
- [ ] Existing HTTP and outbound Rebus behavior remains compatible in Rebus
  mode.
- [ ] The [task board](../README.md) status for AZM-13 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
