# AZM-02 — Transport-neutral message contracts and host metadata

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-01
**Scope**: FRAMEWORK API + GENERATOR MODEL
**Design**: [Contract model](../../azure-functions-messaging-design.md#3-contract-model), [Host roles](../../azure-functions-messaging-design.md#host-roles), [Proposed API shape](../../azure-functions-messaging-design.md#proposed-api-shape)

## Problem

Rebus currently owns the only queue declaration attribute. The messaging
feature needs transport-neutral message and event metadata plus an
assembly-level host identity without coupling Application contracts to Azure
SDK types.

## Prerequisites

- Read [`azure-functions-messaging-design.md`](../../azure-functions-messaging-design.md).
- Preserve `[RebusMessage]` behavior and generated Rebus routing.

## Execution map

- **Public API**: place `[Message]`, `[Event]`, logical-name/alias members, and
  the assembly-level `[MessagingHost]` in `Ark.Tools.MediatorFramework`. The
  host declaration is not Functions-specific; Azure Functions is one hosting
  adapter over it.
- **Generator inputs**: extend the Azure Functions generator's immutable
  contract/host models; reuse existing symbol-display and diagnostic helpers.
- **Generated artifact**: emit a deterministic metadata descriptor only; do
  not emit Service Bus trigger methods yet.
- **Compatibility**: leave Rebus consumption of the new metadata to AZM-14;
  only ensure legacy Rebus generation remains unchanged here.
- **Tests**: use compile-time generator fixtures for every diagnostic and API
  surface snapshots for every public member.
- **Analyzer release metadata**: record portable-name and ownership diagnostics
  in `AnalyzerReleases.Unshipped.md`.

## Implementation steps

1. Add separate public attributes for messages and events. A message requires
   one non-blank owner/destination queue; an event requires one non-blank
   owner/publisher identity.
2. Add the assembly-level `[MessagingHost]` declaration with an optional
   identity, an optional role (`Consumer` default, `Producer`), a referenced
   network profile, explicitly received message contracts, and selected event
   subscriptions. A missing identity is a valid sender-only host. A `Producer`
   role with a named identity grants event-publish ownership only: it owns no
   queue, receives nothing, and declares no received contracts or
   subscriptions — any such declaration is a diagnostic. The attribute never
   selects or registers handlers; developers register handlers in
   SimpleInjector and runtime composition validates normal handler
   registrations. Do not add any Azure or transport member to this attribute:
   the Azure Functions trigger binding (Service Bus or Storage Queue) is
   selected through a dedicated assembly-level attribute in the Azure
   Functions package, consumed by the generator (AZM-10/AZM-11) — not by the
   transport-neutral host declaration.
3. Validate names, duplicate declarations, message/event misuse, and
   conflicting explicit serialization settings at compile time where possible.
   Message owner queues, event publisher identities, and named host identities
   must follow the portable queue-name convention: 3–63 lowercase ASCII
   letters, digits, or hyphens; alphanumeric first/last character; no
   consecutive hyphens. Diagnose every violation. A received message contract
   is valid only when its owner queue equals the host identity ordinally.
   Validate usage against the referenced network's declared capabilities: a
   named consumer identity requires `Receive`; any subscription or `[Event]`
   usage requires `PubSub`. Emit a diagnostic naming the missing capability.
   Never validate against a transport — transports are unknown at compile
   time.
4. Add immutable internal models consumed by the generator and runtime
   manifest emitter. Do not expose Roslyn symbols or Azure SDK types.
5. Add XML documentation and API-surface entries for every public member.
6. Add compatibility tests proving existing Rebus attributes and generated
   Rebus routing remain unchanged.
7. Require event contracts to implement their Mediator command contract and
   diagnose invalid event contract shapes. Handler discovery is not a
   generator concern.
8. Add optional `Name` and `FormerNames` to both contract attributes. Default
   `Name` to the namespace-qualified CLR type name without assembly version.
9. Validate duplicate names/aliases, alias cycles, aliases colliding with
   current names, and Azure topic-name normalization collisions.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
transport-neutral message/event declarations, host identity, subscriptions,
and the reference to a shared network profile.

## Sample extension

Extend the Book sample contracts with `[Message]`/`[Event]` metadata and one
`[MessagingHost]` declaration referencing the AZM-01 network profile, without
changing the existing Book handlers. Everything compiles; no messaging runtime
exists yet, so no runtime fixture is added in this task.

## Required test coverage

- Missing/blank owner and publisher diagnostics.
- One contract cannot be both a message and an event.
- Host identity and subscription declarations are deterministic.
- Identity-less hosts generate no receive queue or subscription declarations.
- Explicit contract protocol plus conflicting network default is diagnosed.
- Existing Rebus-only contracts continue to compile and route.
- A named consumer identity on a network without `Receive` is diagnosed naming
  the capability; a subscription or `[Event]` usage on a network without
  `PubSub` is diagnosed naming the capability.
- A `Producer`-role host declaring received contracts or subscriptions is
  diagnosed; a valid producer declaration is deterministic and owns no queue.
- Named hosts select only message contracts whose owner queue equals the host
  identity; identity-less hosts cannot select received contracts or
  subscriptions.
- Portable queue-name diagnostics cover length, case, characters, edge
  hyphens, and consecutive hyphens.
- Current names and former-name aliases resolve deterministically.

## Outcomes

- Contracts can declare destination queues or event publishers without Azure or
  Rebus dependencies.
- A host can declare identity and subscriptions at assembly scope, validated
  against the network's declared capabilities.
- Generator and runtime work from one validated metadata model.

## Acceptance

- [ ] Message and event attributes are public, documented, validated, and
  covered by API-surface tests.
- [ ] Host identity and event subscription metadata are deterministic.
- [ ] Invalid ownership, duplicate, and protocol-conflict diagnostics are stable.
- [ ] Existing Rebus generation and tests remain behaviorally compatible.
- [ ] The [task board](../README.md) status for AZM-02 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
