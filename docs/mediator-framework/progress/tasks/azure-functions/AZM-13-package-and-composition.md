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

- **Projects**: finalize the package split:
  `Ark.Tools.MediatorFramework.Messaging` (transport-neutral runtime:
  network options, envelope, codecs, pipeline contracts, DataBus, transports, bus,
  dispatcher, lifecycle) and `Ark.Tools.MediatorFramework.AzureFunctions`
  (trigger generation and Functions hosting adapters, depending on the
  messaging package), plus
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators`, central package
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

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. `MessagingNetworkOptions` and its
`Validate` method are the real `Ark.Tools.MediatorFramework.Messaging` types;
codec registry, bus, and step registration names are conceptual seams from
AZM-04/06/08.

Functions host composition root (SimpleInjector, following the existing
Functions HTTP composition — one container, no duplicated application
registrations), for the app bound by
`[assembly: MessagingFunctionsHost(typeof(PrintingParticipant),
MessagingFunctionsTriggerBinding.ServiceBus, ...)]`:

```csharp
/// <summary>Composes the messaging runtime for the bound Functions participant.</summary>
public static void AddArkMessagingFunctionsHost(
    this Container container, IConfiguration configuration)
{
    // Immutable network options resolved from the [MessagingNetwork] declaration
    // (generated registry accessor; conceptual name).
    var options = BookMessagingNetwork.GetNetworkOptions();

    // Runtime transport selection; the participant declaration never names Azure.
    var transport = new ServiceBusMessagingTransport(
        new ServiceBusClient(configuration["Messaging:ConnectionString"]));

    // Startup validations — each fails fast with an explicit diagnostic:
    // 1. Capability check: throws naming any capability the transport lacks.
    options.Validate(transport.Capabilities);

    // 2. The composed transport must match the trigger binding recorded in the
    //    generated manifest (the attribute and this code are different files that
    //    can drift); the InMemory receive transport is rejected outright.
    if (ArkGeneratedFunctions.Manifest.TriggerBinding
        != MessagingFunctionsTriggerBinding.ServiceBus)
    {
        throw new InvalidOperationException(
            "Composed transport 'AzureServiceBus' does not match the generated trigger "
            + $"binding '{ArkGeneratedFunctions.Manifest.TriggerBinding}'.");
    }

    // 3. Exactly one MessagingFunctionsHost binding per Functions app; a composed
    //    descriptor differing from the single generated binding is rejected.
    // 4. Installed codecs must cover the participant's declared Serializers set.
    // 5. Every Processes/Subscribes contract resolves exactly one handler at startup.
    // 6. No Rebus worker, Rebus outbox processor, or native outbox processor starts
    //    in this process; Rebus and native bus registrations are mutually exclusive.

    container.RegisterInstance(options);
    container.RegisterInstance<IMessagingTransport>(transport);

    // Content-type-keyed codec registry; the JSON codec resolves the host's shared
    // JsonSerializerOptions (same options as the HTTP triggers).
    container.RegisterInstance<IMessagingCodecRegistry>(
        MessagingCodecRegistry.Create(new JsonMessagingCodec()));

    // Restricted bus: sender identity = the bound participant identity ("printing").
    container.RegisterInstance<IBus>(
        new MessagingNetworkBus<PrintingParticipant>(transport, options));

    // Host-local pipeline step types from the host binding's IncomingSteps/OutgoingSteps.
    // Each type is registered in the application container and resolved per invocation.
    container.Collection.Register<IMessagingIncomingStep>(
        new[] { typeof(BookUserContextIncomingStep) });
    container.Collection.Register<IMessagingOutgoingStep>(
        new[] { typeof(BookUserContextOutgoingStep) });

    // Pass the declared types and resolve from this same container on each send or delivery.

    // Dispatcher, settlement adapter, DataBus, and resource lifecycle (AZM-12)
    // registrations follow; queue provisioning honors options.ResourceLifecycle.
}
```

Sender-only Minimal API composition — references
`Ark.Tools.MediatorFramework.Messaging` and the generated registry only; no
Functions packages, no dispatch, no triggers, no queues, no subscriptions:

```csharp
// Publisher-only participant ("web-frontend") in a plain Minimal API host.
var options = BookMessagingNetwork.GetNetworkOptions();
var transport = new ServiceBusMessagingTransport(
    new ServiceBusClient(builder.Configuration[options.ConnectionConfigurationKey!]));

// Capability check only: no generated-manifest trigger check applies, because this
// host receives nothing; the transport remains a pure runtime composition decision.
options.Validate(transport.Capabilities);

container.RegisterInstance(options);
container.RegisterInstance<IMessagingTransport>(transport);
container.RegisterInstance<IMessagingCodecRegistry>(
    MessagingCodecRegistry.Create(new JsonMessagingCodec()));
container.RegisterInstance<IBus>(
    new MessagingNetworkBus<WebFrontendParticipant>(transport, options));
// Topic Ensure covers only the events in WebFrontendParticipant.Publishes.

// Application usage: handlers depend on the restricted IBus alone.
app.MapPost("/books/{id}/print-completed", async (int id, IBus bus, CancellationToken ct) =>
{
    await bus.Publish(new BookPrintCompleted(id), cancellationToken: ct).ConfigureAwait(false);
    return Results.Accepted();
});
```

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

- [x] Package and analyzer assets are correct and locked.
- [x] Startup composition is documented and validates configuration.
- [x] No Rebus receiver or outbox processor starts in the Functions process.
- [x] Existing HTTP and outbound Rebus behavior remains compatible in Rebus
  mode.
- [x] The [task board](../README.md) status for AZM-13 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
