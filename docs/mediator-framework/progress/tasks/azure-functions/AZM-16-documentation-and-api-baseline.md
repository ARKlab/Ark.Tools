# AZM-16 — User documentation, migration guidance, and API baseline

**Category**: azure-functions-messaging · **Priority**: release
**Depends on**: AZM-01 through AZM-15, including AZM-07A and AZM-14A
**Scope**: DOCUMENTATION + API REVIEW
**Design**: [Whole design document](../../azure-functions-messaging-design.md), [Test strategy and release gates](../../azure-functions-messaging-design.md#13-test-strategy-and-release-gates)

## Problem

The new transport changes how contracts, hosts, queues, topics, subscriptions,
serialization, failure handling, and scheduling are operated. Users need
explicit guidance and the public API must be reviewable before release.

## Execution map

- **Guide**: integrate task-owned edits in `docs/mediator-framework/guide`;
  remove the design-preview banner only after every example compiles.
- **Sample docs**: update `samples/Ark.MediatorFramework.Sample/README.md` with
  exact commands and the separate Rebus/AMF topology diagrams.
- **API baseline**: update repository API-surface snapshots for every public
  attribute, enum, option, `IBus`, `IFailed<T>`, pipeline, DataBus, outbox,
  processor-hosting, and context member; verify the AZM-03 `MESSAGE`/`EVENT`
  name and alias entries plus the `PARTICIPANT`/`NETWORK` ownership and
  membership entries.
- **Generated examples**: copy from inspected emitted `.g.cs`; do not
  hand-invent trigger signatures.
- **Release gate**: search for stale old task names, direct-subscription claims,
  Rebus interoperability claims, technology-typed network claims,
  network-level pipeline settings, network-level serialization/compression/
  retry settings, contract-level ownership (`OwnerQueue`, `OwnerPublisher`),
  explicit `Role` declarations, passthrough-outbox claims, claims that
  an outbox processor runs inside Functions, and stale host/participant
  terminology (`MessagingHost`, `MessagingFunctionsTrigger`,
  "host identity" meaning a participant).

## Implementation steps

1. Update the Mediator Framework guide with the network member list,
   participant declarations (processes/publishes/subscribes/serializers and
   default identity normalization), inferred roles, subscriptions, shared
   network
   configuration, the capability model with runtime transport selection
   (InMemory, Service Bus, Storage Queue), and the restricted bus API.
   Document the participant/host distinction: a participant is a logical
   network member; a host is the process and hosting technology (Azure
   Functions with generated triggers, a Rebus worker, or a test/custom host
   running the InMemory pump or the outbox processor). InMemory consumer
   participants are never hosted in a Functions app; Azure Functions
   end-to-end testing uses Azurite or the Azure Service Bus emulator.
2. Document ownership: contracts are owner-free; the participant declaring a
   message in `Processes` owns it (destination = its identity queue); the
   participant declaring an event in `Publishes` owns its topic; subscribers
   get independent copies in their own identity queues. Document wire-protocol
   ownership: the processing participant's default serializer for messages,
   the publisher's for events, and the strict subscriber-compatibility rule.
3. Document header-driven protocol reads, protocol retirement behavior, and
   conflict diagnostics. Explain that generated metadata maps `typeof(T)` to
   the write name and a received name to closed generic serde/processor calls,
   following the same no-reflection boundary as generated Minimal API and
   HttpTrigger binding and response serialization. Document that headers and
   transport-owned body sequences remain separate, and that JSON uses the
   host's source-generated `JsonSerializerOptions` without leaking JSON
   metadata into protocol-neutral codec APIs.
4. Document at-least-once delivery, fail-fast DLQ, retry exhaustion, native
   delivery-count semantics, PeekLock requirements, inline second-level
   dispatch, no persisted `IFailed<T>` message, and separate scopes.
5. Document resource lifecycle, IaC coexistence, ownership-safe subscription
   removal, and local testing limitations.
6. Document the host-local incoming/outgoing pipeline (registered on the host
   binding), opt-in user/OTel
   propagation, additional headers, per-participant compression, DataBus
   claim-check,
   provider-specific minimum lifetime, and the Azure Blob IaC lifecycle
   prerequisite.
7. Document that request/reply and delayed publish are out of scope; document
   each transport's capability set (Storage Queue has no `PubSub`; its DLQ is
   the framework-managed poison queue).
8. Review API-surface snapshots and generated-source examples, including the
   deterministic `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` entries
   implemented by AZM-03.
9. Add migration guidance from Rebus-only receive hosts to Functions hosts,
   explicitly stating that Rebus remains supported, the new ownership metadata
   is shared, and persisted-message interoperability is unsupported.
10. Document generated Rebus host assistance from shared network/participant
    definitions: owner routing, participant-filtered dispatch adapters,
    post-start
    subscriptions, exact retry mapping, and the requirements descriptor.
    State that generators see contracts only, adapters dispatch through
    `IRequestProcessor`/`ICommandProcessor`, and developers register all
    application handlers.
    Include the explicit non-generated boundary for serializer, `RetryDelay`,
    compression implementation, DataBus provider, transport, workers,
    pipeline, subscription storage, logging, timeouts, and outbox ownership.
11. Document durable SQL outbox support for native `Send`/`Publish`, the
    separate always-running `outbox-processor` `IHostedService`, its reserved
    identity, and why Functions only enqueue. State that the registered bus
    backend selects the Rebus or native producer/processor and that the
    existing WebInterface and RebusProcessor retain their real Rebus outbox
    registrations.
12. Document logical contract names, `FormerNames`, and the explicit topology
    migration required when an event topic name changes.
13. Document the portable queue-name convention and analyzer diagnostics.

## Guide contribution

This task integrates and reviews the guide sections contributed by AZM-01
through AZM-15. It must not replace their task-owned documentation with a
separate summary.

## Sample extension

Update the existing Book sample README and navigation so users can run the
same background activity through Rebus or Azure Functions and understand which
receiver owns the queue in each mode.

## Required test coverage

- Documentation examples compile in representative fixtures.
- API baseline includes all public attributes, options, bus methods, and failure
  abstractions plus canonical message/event names, aliases, participant
  declarations, and network member lists.
- Generated examples match actual routes, headers, and resource names.
- No documentation claims unsupported emulator or Rebus interoperability.
- Composition examples preserve the WebInterface Rebus outbox with its
  processor disabled and the RebusProcessor outbox with its processor enabled.
- Native composition examples place `outbox-processor` in a separate
  always-running host and never in Azure Functions.
- Rebus examples show publisher-only and consumer generated
  setup, including awaited subscriptions, without hiding runtime
  infrastructure choices.

## Outcomes

- The feature is operationally understandable and reviewable.
- Public API and generated output changes are intentional and stable.

## Acceptance

- [ ] Guide and migration documentation cover all supported and unsupported paths.
- [ ] API-surface snapshots are updated and reviewed.
- [ ] Examples are consistent with generated code and sample behavior.
- [ ] Rebus compatibility boundaries and the Rebus/native durable-outbox
  hosting distinction are explicit.
- [ ] The [task board](../README.md) status for AZM-16 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
