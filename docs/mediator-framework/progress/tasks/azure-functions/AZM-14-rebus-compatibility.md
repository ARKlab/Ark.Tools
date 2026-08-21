# AZM-14 — Rebus compatibility and generated host setup

**Category**: azure-functions-messaging · **Priority**: compatibility
**Depends on**: AZM-02, AZM-08, AZM-09, AZM-13
**Scope**: FRAMEWORK API + REBUS GENERATOR + REBUS ADAPTER + SAMPLE COMPOSITION
**Design**: [Sample proof](../../azure-functions-messaging-design.md#12-sample-proof), [Restricted bus shim](../../azure-functions-messaging-design.md#9-restricted-bus-shim)

## Problem

The Book application currently depends directly on Rebus `IBus` and Rebus
`IFailed<T>`. The same application handlers cannot run on a Mediator Framework
network until those APIs are transport-neutral. Rebus and Mediator Framework
persisted messages remain wire-incompatible and must never share one logical
bus. The sample also manually stitches generated routing into two Rebus hosts
and has no generated event-subscription setup. The shared network and
participant
definitions must assist both Rebus compositions without taking ownership of
their infrastructure.

## Execution map

- **Rebus projects**: update `Ark.Tools.MediatorFramework.Rebus` and
  `Ark.Tools.MediatorFramework.Rebus.Generators`; preserve
  `RebusMessageAttribute` as a supported legacy surface.
- **Application project**: replace `Rebus.Bus.IBus` and
  `Rebus.Retry.Simple.IFailed<T>` dependencies in
  `Ark.MediatorFramework.Sample.Application` with framework abstractions.
- **Rebus participant metadata**: both sample Rebus hosts bind to participants
  of the same messaging
  network with distinct inferred roles:
  `WebInterface` binds a publisher-only participant; `RebusProcessor` binds
  the consumer participant.
- **Generated API**: generate participant-specific routing, framework-owned
  Rebus
  dispatch adapters, post-start event subscriptions, exact retry options, and
  an immutable requirements descriptor. Follow the existing
  `ConfigureArkRebusRouting<TAssemblyMarker>` pattern and keep every public
  generated member XML-documented.
- **Handler boundary**: generators inspect contracts and network/participant
  metadata
  only. They never discover, reference, validate, or register application
  handler implementations. Developers register handlers in the application
  container; generated adapters dispatch through
  `IRequestProcessor`/`ICommandProcessor`.
- **Runtime-owned composition**: transport/credentials, subscription storage,
  serializer, concrete DataBus, compression implementation, logging,
  worker/concurrency settings, timeouts, and outbox processor ownership remain
  explicit host code.
- **Rebus outbox**: preserve the existing
  `ApplicationComposition.ConfigureRebusOutbox` calls in
  `Ark.MediatorFramework.Sample.WebInterface/SampleComposition.cs` and
  `Ark.MediatorFramework.Sample.RebusProcessor/RebusProcessorComposition.cs`;
  the participant's declarations alone must not infer whether an outbox
  processor starts.
- **Stop condition**: no AMF/Rebus header translation and no attempt to consume
  a persisted message produced by the other stack. Do not generate a complete
  Rebus configuration or silently select infrastructure.

## Implementation steps

1. Move the restricted `IBus` and `IFailed<T>` contracts to a
   transport-neutral Mediator Framework package.
2. Make the Rebus generator consume the network's member-derived contract
   registry
   and the participant declaration bound to the current assembly's host while
   preserving legacy
   `[RebusMessage]` behavior. Diagnose conflicting dual declarations, missing
   network/participant references, a host binding referencing a participant
   listed in no network, and
   subscriptions to
   events no member publishes. Remove handler-symbol discovery and generated
   missing-handler verification from this path.
3. Generate owner routing for every processed message, targeting the
   processing participant's identity queue. Preserve
   `ConfigureArkRebusRouting<TAssemblyMarker>` as the compatibility entry point
   and make it derive routes from the generated registry.
4. Generate participant-filtered Rebus dispatch adapters solely from contract
   metadata:
   - a participant with no `Processes`/`Subscribes` emits/registers no receive
     adapters;
   - a consumer participant emits contract adapters for exactly its
     `Processes` messages;
   - it also emits contract adapters for its `Subscribes` events.
   Each adapter depends only on `IRequestProcessor` or `ICommandProcessor` and
   dispatches the received contract through that processor. A generated
   registration method may register only these framework-owned adapters with
   Rebus/SimpleInjector; it must not register or verify application handlers.
   Developers keep application-handler registration in their composition root.
5. Generate an async post-start subscription method that invokes Rebus
   `Subscribe<TEvent>` once for every event in the consumer participant's
   `Subscribes`. Hosts bound to sender-only/publisher participants emit a
   no-op method. Subscription
   storage remains a required runtime configuration.
6. Generate an options extension that maps the bound participant's
   `MaximumDeliveryCount` and `SecondLevelRetriesEnabled` to
   `ArkRetryStrategy`. Preserve explicit runtime configuration for error queue
   name, error-detail bounds, cooldown, and Rebus-only options that do not
   alter the mapped attempt counts.
7. Generate an immutable Rebus host requirements descriptor containing the
   bound participant's
   inferred roles/identity, input queue name when applicable, subscribed event
   types,
   `MaximumHandlerDuration`, and whether compression/DataBus are required.
   Runtime composition uses it for validation and diagnostics.
8. Do not automatically map:
   - `RetryDelay`, because Rebus retry/defer semantics differ from Storage
     Queue visibility delay;
   - serialization, because Rebus selects one serializer while the native
     envelope supports header-driven multi-protocol reads;
   - compression algorithm/threshold until an exact Rebus mapping is proven;
   - DataBus provider/store/credentials or attachment semantics;
   - participant-local incoming/outgoing steps, because Rebus pipeline anchors
     differ.
   For compression and DataBus, require explicit runtime callbacks/registration
   acknowledgements when the generated requirements descriptor says they are
   needed, and fail composition with a targeted diagnostic when they are
   absent. Do not attempt to infer provider registration from Rebus internals.
9. Register a Rebus `IBus` adapter that proxies `Send`, delayed `Send`,
   `Publish`, optional `Dictionary<string, string>` additional headers, and
   cancellation to the supported Rebus APIs. Rebus composition enforces the
   same declaration-based publish rule through the bound participant; a
   participant that does not declare the event in `Publishes` cannot publish
   it.
10. Map Rebus `IFailed<T>` to the framework `IFailed<T>` so application failure
   handlers contain no Rebus types.
11. Keep Rebus headers, wire serialization, pipeline implementations,
    transport, worker, DataBus provider, and outbox runtime independent from
    the native Mediator Framework transport. Document that the stacks are not
    wire-interoperable; do not test for the absence of interoperability,
    because it is neither required nor expected.
12. Keep `WebInterface` registered as a Rebus one-way sender bound to the
   publisher-only participant with
    `ConfigureRebusOutbox(..., startProcessor: false)`.
13. Keep `RebusProcessor` registered as the consumer's Rebus host with
   `ConfigureRebusOutbox(..., startProcessor: true)` so it continues to run
   the durable Rebus outbox processor. Preserve existing SQL and in-memory
   outbox profiles and their cleanup/processing behavior.
14. Make native Mediator Framework and Rebus bus compositions mutually
   exclusive for one logical topology.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. The generated members extend the existing
`Ark.MediatorFramework.Generated.ArkGeneratedEndpoints` static partial class
emitted by `Ark.Tools.MediatorFramework.Rebus.Generators`, which already
exposes `ConfigureArkRebusRouting<TAssemblyMarker>`.

The conceptual generated Rebus-assistance API (from the design, verbatim):

```csharp
ArkGeneratedEndpoints.ConfigureArkRebusRouting<TAssemblyMarker>(routing);
ArkGeneratedEndpoints.RegisterArkRebusDispatchAdaptersForParticipant<TAssemblyMarker>(
    container);
ArkGeneratedEndpoints.ConfigureArkRebusOptionsForParticipant<TAssemblyMarker>(options);
await ArkGeneratedEndpoints
    .SubscribeArkRebusEventsForParticipantAsync<TAssemblyMarker>(bus, cancellationToken)
    .ConfigureAwait(false);

var requirements =
    ArkGeneratedEndpoints.GetArkRebusParticipantRequirements<TAssemblyMarker>();
```

Composing a Rebus consumer host with the generated assistance — every
infrastructure choice (transport, connection, serializer, subscription
storage, workers, outbox) stays explicit host code:

```csharp
// RebusProcessor composition root: binds the consumer participant of the shared network.
var requirements = ArkGeneratedEndpoints.GetArkRebusParticipantRequirements<Program>();

// Framework-owned dispatch adapters for exactly the participant's declared contracts.
ArkGeneratedEndpoints.RegisterArkRebusDispatchAdaptersForParticipant<Program>(container);

// Application handlers remain developer-registered; the generator never sees them.
container.Register<ICommandHandler<PrintBook>, PrintBookHandler>();
container.Register<ICommandHandler<BookPrintCompleted>, RecordPrintCompletionHandler>();

var bus = Configure.With(activator)
    .Transport(t => t.UseAzureServiceBus(connectionString, requirements.InputQueueName))
    .Routing(r => ArkGeneratedEndpoints.ConfigureArkRebusRouting<Program>(r))
    .Options(o =>
    {
        // Exact N / second-level mapping generated from the participant's retry policy.
        ArkGeneratedEndpoints.ConfigureArkRebusOptionsForParticipant<Program>(o);
        // Rebus-only options that do not alter mapped attempt counts stay explicit here.
    })
    .Subscriptions(s => /* explicit subscription storage */ s.StoreInSqlServer(...))
    .Start();

// Subscriptions are an explicit post-start async operation; no-op for
// sender-only/publisher participants.
await ArkGeneratedEndpoints
    .SubscribeArkRebusEventsForParticipantAsync<Program>(bus, cancellationToken)
    .ConfigureAwait(false);

// requirements.MaximumHandlerDuration, requirements.RequiresCompression, and
// requirements.RequiresDataBus drive startup validation: missing runtime
// callbacks/registrations fail composition with a targeted diagnostic.
```

Generated dispatch adapter for one contract — implements the Rebus handler
interface, depends only on `ICommandProcessor`, and delegates dispatch (no
application-handler discovery):

```csharp
// <auto-generated />
#nullable enable
namespace Ark.MediatorFramework.Generated;

/// <summary>Rebus dispatch adapter for the "books.print_book" contract of "printing".</summary>
public sealed class ArkRebusDispatchAdapter_PrintBook
    : global::Rebus.Handlers.IHandleMessages<global::Sample.Contracts.PrintBook>
{
    private readonly global::Ark.Tools.Solid.ICommandProcessor _processor;

    /// <summary>Creates the adapter over the application command processor.</summary>
    public ArkRebusDispatchAdapter_PrintBook(global::Ark.Tools.Solid.ICommandProcessor processor)
    {
        _processor = processor;
    }

    /// <summary>Dispatches the received contract through the command processor.</summary>
    public async global::System.Threading.Tasks.Task Handle(
        global::Sample.Contracts.PrintBook message)
    {
        // PrintBook : ICommand<PrintBook>, so the generic overload resolves the handler
        // at compile time with no reflection.
        await _processor.ExecuteAsync(message, default).ConfigureAwait(false);
    }
}
```

Generated retry mapping onto the real `Ark.Tools.Rebus`
`ArkRetryStrategyConfigurationExtensions.ArkRetryStrategy` extension:

```csharp
// Generated body of ConfigureArkRebusOptionsForParticipant<TAssemblyMarker> (conceptual),
// for a participant whose IMessagingRetryPolicy declares
// MaximumDeliveryCount = 5 and SecondLevelRetriesEnabled = true:
public static void ConfigureArkRebusOptionsForParticipant<TAssemblyMarker>(
    global::Rebus.Config.OptionsConfigurer options)
{
    options.ArkRetryStrategy(
        maxDeliveryAttempts: 5,
        secondLevelRetriesEnabled: true);
    // Not mapped, by design: errorQueueName, errorDetailsHeaderMaxLength,
    // errorTrackingMaxAgeMinutes, and errorQueueErrorCooldownTimeSeconds remain
    // runtime-owned; RetryDelay is never mapped because Rebus retry/defer semantics
    // differ from the native Storage Queue visibility delay.
}
```

## Guide contribution

Update [`guide/rebus.md`](../../../guide/rebus.md),
[`guide/azure-functions.md`](../../../guide/azure-functions.md), and
[`guide/host-setup-and-composition.md`](../../../guide/host-setup-and-composition.md)
with the common application APIs, separate topology modes, and
non-interoperability. Document the generated-assistance matrix and show that
subscriptions run after bus start while infrastructure remains explicit.

## Sample extension

Update the Book application handlers to depend only on the framework `IBus`
and `IFailed<T>`. Keep the existing WebInterface and RebusProcessor durable
Rebus outbox registrations as-is behind the Rebus adapter. Bind both Rebus
hosts to participants of the shared network:

- `WebInterface`: binds the publisher-only participant; generated
  routing/options/requirements, no Rebus receive adapters, input queue, or
  subscriptions.
- `RebusProcessor`: binds the consumer participant; generated dispatch
  adapters for exactly its `Processes`/`Subscribes`, routing, retry options,
  and post-start subscriptions.

Replace the hand-written `SampleRebusEndpoints` forwarding helper with the
generated Rebus host APIs. Keep application-handler registration, serializer,
transport, outbox, user-context pipeline, worker count, subscription storage,
and provider callbacks visible in the composition roots. In native Mediator
Framework mode the WebInterface composes the configured framework `IBus`.
Native SQL outbox integration is owned by AZM-14A.

## Required test coverage

- Legacy `[RebusMessage]` routing remains compatible.
- Member-derived ownership metadata drives Rebus routing without Azure
  types.
- Conflicting legacy/new routing metadata is diagnosed.
- Generator inputs contain contracts/network/participant metadata and no
  application
  handler symbols.
- Sender-only/publisher participants generate routes but no receive adapters
  or subscriptions.
- Consumer message dispatch adapters are exactly the participant's
  `Processes` messages.
- Consumer event dispatch adapters/subscriptions are exactly its declared
  `Subscribes`.
- Generated adapters resolve only `IRequestProcessor`/`ICommandProcessor`;
  application handlers are developer-registered and never emitted or
  registered by the generator.
- Generated subscriptions are awaited after bus start and are no-ops for
  sender-only/publisher participants.
- Generated retry options map maximum attempts and second-level enablement
  exactly to `ArkRetryStrategy`.
- Requirements expose handler duration and compression/DataBus needs; missing
  required runtime callbacks fail startup explicitly.
- Serializer, `RetryDelay`, transport, workers, pipeline, outbox processor,
  subscription storage, and provider credentials are not silently generated.
- Rebus adapter preserves supported send, publish, delay, additional headers,
  sender identity, and cancellation.
- Rebus and Mediator Framework `IFailed<T>` reach the same application failure
  handler.
- WebInterface keeps the real Rebus outbox with no local outbox processor.
- RebusProcessor keeps the real Rebus outbox with its processor enabled.
- Rebus and native Mediator Framework bus registrations conflict explicitly
  for one logical topology.

## Outcomes

- Book application handlers are transport-neutral.
- Both Rebus hosts are assisted by the shared network/participant definitions
  without
  hiding infrastructure composition.
- Application handler discovery and registration remain developer-owned.
- Rebus retains its durable outbox and richer feature set.
- Mediator Framework networks remain separate, non-interoperable topologies.

## Acceptance

- [ ] Application code contains no Rebus `IBus` or Rebus `IFailed<T>` dependency.
- [ ] Rebus adapters preserve existing behavior and legacy metadata.
- [ ] Sender-only/publisher and consumer Rebus setup is generated from
  network/participant
  definitions, including routing, filtered dispatch adapters, subscriptions,
  and exact retry mapping.
- [ ] Generators see only contracts and dispatch through processors; application
  handlers are registered explicitly by developers.
- [ ] Non-equivalent/provider-specific settings remain explicit and are
  validated through generated requirements.
- [ ] Existing WebInterface and RebusProcessor Rebus outbox registrations and
  processing behavior remain unchanged.
- [ ] Tests prove the two topology modes separately; non-interoperability is
  documented, not tested.
- [ ] The [task board](../README.md) status for AZM-14 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
