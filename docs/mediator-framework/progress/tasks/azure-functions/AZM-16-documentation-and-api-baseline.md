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
  attribute, enum, option, `IBus`, `MessagingFailed<T>`, pipeline, DataBus, outbox,
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
   conflict diagnostics.
4. Document at-least-once delivery, fail-fast DLQ, retry exhaustion, native
   delivery-count semantics, PeekLock requirements, inline second-level
   dispatch, no persisted `MessagingFailed<T>` message, and separate scopes.
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

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. This task is documentation-oriented, so the
shapes below are the baseline-regeneration commands and the doc checklist.

Regenerating and accepting `ArkApiSurface.txt` baselines (per the repository
API-surface workflow; `ARKAPI001` = missing baseline, `ARKAPI002` = drift):

```text
# 1. Build the project with generated-file emission to refresh the snapshot:
dotnet build <project>.csproj -p:EmitCompilerGeneratedFiles=true

# 2. Review obj/Debug/<tfm>/ArkApiSurface.current.txt, including the deterministic
#    MESSAGE / EVENT / PARTICIPANT / NETWORK entries (AZM-03) and generated
#    messaging contract/interaction lines; host trigger details stay excluded.

# 3. Only after approval, copy the snapshot over the committed baseline:
copy obj\Debug\net10.0\ArkApiSurface.current.txt ArkApiSurface.txt

# 4. Verify the release gates:
dotnet build Ark.Tools.slnx --configuration Debug
dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1
```

Release-gate documentation checklist (each item is verified, not assumed):

```text
[ ] Every generated example is copied from inspected emitted .g.cs output
    (obj/<Configuration>/<tfm>/generated/...), never hand-invented.
[ ] Wire identity examples use logical snake_case names ("books.print_book"),
    never CLR type names.
[ ] Retry examples use the N / 2N model and the fixed per-transport abandon
    semantics (Service Bus immediate; Storage Queue visibilityTimeout = RetryDelay).
[ ] Storage Queue docs state the single-Base64 messageEncoding "none" contract
    and the 46 080 / 49 152 canonical caps.
[ ] No stale terminology: MessagingHost, MessagingFunctionsTrigger, OwnerQueue,
    OwnerPublisher, explicit Role declarations, "host identity" as participant.
[ ] No Rebus wire-interoperability, direct-subscription, technology-typed
    network, or Functions-hosted outbox-processor claims.
[ ] The design-preview banner is removed only after every example compiles.
```

Sample docs cross-link table (verified while integrating task-owned guide
sections):

| Doc | Must link to | Owned by |
| --- | --- | --- |
| `guide/azure-functions.md` | Trigger model, host binding attribute, Storage Queue host.json contract, resource lifecycle | AZM-10/11/12/13 |
| `guide/host-setup-and-composition.md` | Functions/sender-only composition, outbox hosting split | AZM-13/14A |
| `guide/rebus.md` | Generated Rebus assistance, adapter `IBus`/`MessagingFailed<T>`, non-interoperability | AZM-14 |
| `guide/api-surface-snapshots.md` | MESSAGE/EVENT/PARTICIPANT/NETWORK lines, generated trigger entries | AZM-03/16 |
| `samples/Ark.MediatorFramework.Sample/README.md` | Three-participant topology, two topology modes, `outbox-processor` host commands | AZM-15/16 |

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
