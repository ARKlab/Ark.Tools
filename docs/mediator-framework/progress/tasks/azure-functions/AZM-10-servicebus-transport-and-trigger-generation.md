# AZM-10 — Azure Service Bus transport and trigger source generation

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-05, AZM-08, AZM-09
**Scope**: TRANSPORT + GENERATOR
**Design**: [Transport abstraction](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport), [Generated Functions surface](../../azure-functions-messaging-design.md#6-generated-functions-surface)

## Problem

Production participants need the Azure Service Bus transport plus generated
Azure
Functions triggers. Trigger attributes are compile-time facts, so a Functions
host running a receive-capable participant selects its trigger binding at
compile time
through a dedicated assembly-level attribute in the Azure Functions package,
while the send side and non-Functions test hosts keep selecting
the transport at runtime composition. Because the AZM-09 dispatcher
already works, generated triggers are never emitted without a working
dispatcher: this task is a single unit so the codebase never contains
dispatcher-less trigger code.

## Execution map

- **Transport**: implement the Service Bus transport
  (`Capabilities = Receive | PubSub | ScheduledSend`, hard 256 KB total
  standard-tier message limit including application properties) in
  `Ark.Tools.MediatorFramework.Messaging` using the AZM-05 contract:
  envelope-to-message mapping via application properties and binary body,
  native scheduling, topic publish, and PeekLock settlement mapped to
  complete/abandon/dead-letter with the native `DeliveryCount`. Producer-only
  participants reference only this messaging package. Its AZM-05 measurement
  must evaluate the full native message, not body bytes alone.
- **Generator project**:
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators`. Generated methods
  call the existing AZM-09 runtime dispatcher; generated source contains no
  codec, pipeline, retry, or DI logic. The Functions-binding settlement
  adapter lives in `Ark.Tools.MediatorFramework.AzureFunctions`, which
  references the messaging package.
- **Trigger selection**: emit the Service Bus trigger only when the Functions
  host assembly binds itself to a consumer participant through the
  `[assembly: MessagingFunctionsHost(typeof(PrintingParticipant), MessagingFunctionsTriggerBinding.ServiceBus)]`
  attribute defined in `Ark.Tools.MediatorFramework.AzureFunctions`; the
  Storage Queue selection is handled by AZM-11. A Functions host assembly
  binding a consumer participant without this attribute, or binding a
  participant listed in no network, is a
  compile-time diagnostic.
- **Output per Functions app**: exactly one bound
  `[MessagingFunctionsHost]` participant is permitted. That participant emits
  zero or one identity-queue trigger plus one deterministic desired-resource
  manifest. Multiple messaging participant bindings are a compile-time
  diagnostic.
- **Binding verification**: inspect the exact installed
  `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` API before emitting
  attributes; add a compile fixture using the actual package.
- **Conformance**: run the AZM-05 transport conformance suite against the
  Service Bus transport against the Azure Service Bus emulator (Docker) or a
  live namespace; absence of
  infrastructure is explicit, never a silent skip.
- **Generated-code gate**: after building, inspect emitted `.g.cs` in the
  boundary test host and sample as required by repository policy.
- **Runnable state**: triggers dispatch through the proven runtime at task
  end; full solution builds and tests green without Azure credentials.

## Implementation steps

1. Implement the Service Bus transport send path: envelope headers to
   application properties, binary body, scheduled enqueue for delayed send,
   and topic publish.
2. Implement the receive-side settlement adapter mapping the Functions
   Service Bus binding objects (message + message actions) onto the AZM-05
   locked-delivery contract consumed by the dispatcher.
3. Extend the incremental generator with contract, network, and participant
   metadata
   inputs, honoring the Functions host binding attribute, which references the
   participant type, selects the trigger binding, and may add host-local steps
   (the transport-neutral `[MessagingParticipant]` attribute from AZM-02 has
   no trigger or step members). A Functions app may bind exactly one messaging
   participant; diagnose multiple `[MessagingFunctionsHost]` bindings before
   generating source.
4. Emit one stable trigger for the bound participant's identity queue when the
   participant declares `Processes` or `Subscribes` and the assembly declares
   the Service
   Bus trigger selection. A bound participant with no `Processes` and no
   `Subscribes` is a send-only Functions host:
   emit an information diagnostic and no trigger.
   Do not discover handler registrations in the
   generator; dispatch always goes through the processors.
5. Emit subscription manifest entries that forward each subscribed event into
   the participant identity queue. Do not emit direct subscription triggers.
6. Emit a deterministic resource/subscription manifest for startup management
   (consumed by AZM-12). Record the selected trigger binding in the manifest
   so AZM-13 startup composition can fail when the composed runtime transport
   does not match it.
7. Emit thin async methods that pass the exact Azure binding object and
   cancellation token to the runtime dispatcher.
8. Diagnose missing owners/publishers, duplicate routes, invalid names,
   duplicate subscription declarations, capability-usage violations,
   conflicting protocol settings, and unsupported contract shapes.
9. Generated triggers must use PeekLock, set `AutoCompleteMessages = false`,
   bind `ServiceBusMessageActions`, and pass manual settlement to the
   runtime. ReceiveAndDelete is rejected. Complete maps to
   `CompleteMessageAsync`. Abandon maps to immediate `AbandonMessageAsync`
   — Service Bus cannot delay abandon beyond the five-minute PeekLock cap,
   so `RetryDelay` is ignored and a retry storm is accepted. Fail-fast and
   missing-`IFailed` DLQ map to `DeadLetterMessageAsync` with bounded
   reason and description. Apply entity `MaxDeliveryCount = 2N` when the
   participant's retry policy enables second-level retries, otherwise `N`.
   `maxAutoLockRenewalDuration` must cover `MaximumHandlerDuration`.
10. Add API-surface snapshot lines for generated messaging triggers and
    routing.

Participants that consume nothing (no `Processes`, no `Subscribes`) emit no
receive trigger or subscription manifest entry.
Participants composed over InMemory cannot be hosted in Azure Functions: their
assemblies emit no trigger, Functions composition rejects the InMemory receive
transport (AZM-13), and their receive side runs through the runtime pump in a
test or custom host. Azure Functions end-to-end tests use Azurite or the Azure
Service Bus emulator (Docker).

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
generated queue/subscription trigger model, deterministic routes, the
compile-time Functions-host attribute in the Functions host assembly, and
the relationship between participant identity, network declaration, and event
subscriptions.

## Sample extension

Add the Functions host with the generated Service Bus trigger for the Book
consumer participant
beside the existing InMemory-composed fixtures. The sample compiles and its
generated `.g.cs` is inspected; live Azure execution is optional and explicit.

## Required test coverage

- At most one trigger per participant identity queue.
- Multiple `[MessagingFunctionsHost]` bindings in one Functions app are
  diagnosed; one bound sender-only participant remains valid and emits no
  trigger.
- A bound participant with an empty receive set (no `Processes`, no
  `Subscribes`) produces an information diagnostic and no trigger.
- Multiple types in one queue map to typed generated dispatch.
- Same topic subscribed by two participant configurations generates distinct,
  deterministic subscription identities.
- Repeated generator runs produce byte-identical output.
- Invalid and excluded contracts produce the expected diagnostics/no source.
- Portable queue-name violations and ownership/membership diagnostics are
  raised before trigger generation.
- PeekLock is configured and ReceiveAndDelete is rejected.
- Every event subscription forwards to the participant identity queue.
- The manifest records the selected trigger binding deterministically.
- Envelope-to-Service-Bus mapping round-trips headers and binary payloads.
- Settlement adapter maps complete/immediate-abandon/dead-letter (with
  reason) and exposes the native delivery count. No abandon delay is
  implemented or tested.
- Transport conformance suite runs against Service Bus through the Azure
  Service Bus emulator (Docker) or a live namespace, with explicit absence
  reporting.

## Caveats

- Verify exact Worker/Service Bus extension attribute signatures before
  emitting source; do not infer them from memory.
- Generated code must not reference Minimal API runtime types or Rebus handler
  types.

## Outcomes

- A Functions host gets discoverable Service Bus triggers for its consumer
  participant from contract
  metadata, dispatching through the already-proven runtime.
- Trigger source remains thin and reflection-free.
- Startup receives a deterministic desired-resource manifest.

## Acceptance

- [ ] Service Bus transport implements the AZM-05 contract including
  settlement and delivery count.
- [ ] Identity-queue triggers and forwarding subscription manifests are
  deterministic and typed.
- [ ] Generated source awaits runtime dispatch and contains no serializer or
  retry logic.
- [ ] Diagnostics cover all invalid routing and participant-selection cases.
- [ ] API-surface snapshots record generated messaging routes.
- [ ] The [task board](../README.md) status for AZM-10 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
