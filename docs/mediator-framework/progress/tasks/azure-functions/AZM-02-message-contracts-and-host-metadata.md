# AZM-02 — Transport-neutral message contracts and participant metadata

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-01
**Scope**: FRAMEWORK API + GENERATOR MODEL
**Design**: [Contract model](../../azure-functions-messaging-design.md#3-contract-model), [Participant roles](../../azure-functions-messaging-design.md#participant-roles), [Proposed API shape](../../azure-functions-messaging-design.md#proposed-api-shape)

## Problem

Rebus currently owns the only queue declaration attribute. The messaging
feature needs transport-neutral message and event metadata plus an
assembly-level participant identity without coupling Application contracts to
Azure SDK types.

## Prerequisites

- Read [`azure-functions-messaging-design.md`](../../azure-functions-messaging-design.md).
- Preserve `[RebusMessage]` behavior and generated Rebus routing.

## Execution map

- **Public API**: place `[Message]`, `[Event]`, logical-name/alias members, the
  network contract registry, and the assembly-level `[MessagingParticipant]` in
  `Ark.Tools.MediatorFramework`. The participant declaration is not
  Functions-specific; Azure Functions is one hosting adapter over it.
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
   owner/publisher identity. A contract carries either `[Message]` or
   `[Event]`, never both; dual attribution is a diagnostic.
2. Add a deterministic registry of every message and event contract to the
   shared network declaration. A contract may participate only when registered
   in that network; diagnose duplicates and participant references to
   unregistered
   contracts.
3. Add the assembly-level `[MessagingParticipant]` declaration with an optional
   identity, an optional role (`Consumer` default, `Producer`), a referenced
   network profile, selected event subscriptions, and participant-local
   incoming/outgoing step types. There is no `ReceivedContracts` member. A
   named consumer automatically receives every registered message whose owner
   queue equals its identity; event publisher identity never implies event
   receipt. A missing identity is a valid sender-only participant. A
   `Producer` role
   with a named identity grants event-publish ownership only: it owns no queue,
   receives nothing, and declares no subscriptions — any subscription is a
   diagnostic. An assembly declares at most one `[MessagingParticipant]`;
   duplicate declarations are a diagnostic. The attribute never selects or
   registers handlers; developers
   register handlers and step implementations in SimpleInjector and runtime
   composition validates normal registrations. Do not add any Azure or
   transport member to this attribute:
   the Azure Functions trigger binding (Service Bus or Storage Queue) is
   selected through a dedicated assembly-level attribute in the Azure
   Functions package, consumed by the generator (AZM-10/AZM-11) — not by the
   transport-neutral participant declaration.
4. Validate names, duplicate declarations, message/event misuse, and
   conflicting explicit serialization settings at compile time where possible.
   Message owner queues, event publisher identities, and named participant
   identities
   must follow the portable queue-name convention: 3–63 lowercase ASCII
   letters, digits, or hyphens; alphanumeric first/last character; no
   consecutive hyphens. Diagnose every violation. Reserved names are
   diagnosed: the identity `outbox-processor` is reserved for the framework
   outbox processor and is invalid as a participant identity, owner queue, or
   owner publisher, and owner queue names ending in `-poison` are reserved
   for framework-managed companion queues. Derived event topic names longer
   than the Service Bus 260-character entity limit are diagnosed.
   Derive each consumer's
   received message set from registered messages whose owner queue equals the
   participant identity ordinally.
   Validate usage against the referenced network's declared capabilities: a
   named consumer identity requires `Receive`; any subscription or `[Event]`
   usage requires `PubSub`. Emit a diagnostic naming the missing capability.
   Never validate against a transport — transports are unknown at compile
   time.
5. Add immutable internal models consumed by the generator and runtime
   manifest emitter. Do not expose Roslyn symbols or Azure SDK types.
6. Add XML documentation and API-surface entries for every public member.
7. Add compatibility tests proving existing Rebus attributes and generated
   Rebus routing remain unchanged.
8. Require event contracts to implement their Mediator command contract and
   diagnose invalid event contract shapes. Handler discovery is not a
   generator concern.
9. Add optional `Name` and `FormerNames` to both contract attributes. Default
   `Name` to the namespace-qualified CLR type name without assembly version,
   normalized to lowercase snake_case: each namespace/type segment is
   lowercased and PascalCase word boundaries become underscores (for example
   `Books.PrintCompleted` becomes `books.print_completed`). Explicit `Name`
   and `FormerNames` values must already be in normalized form;
   non-normalized values are diagnosed.
10. Validate duplicate names/aliases (including normalization collisions
   between distinct CLR types), alias cycles, and aliases colliding with
   current names.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
transport-neutral message/event declarations, network contract registration,
automatic message receipt by identity queue, explicit event subscriptions,
participant-local pipeline steps, the lowercase snake_case logical-name
normalization, reserved names, and the reference to a shared network profile.

## Sample extension

Extend the Book sample contracts with `[Message]`/`[Event]` metadata, register
them in the AZM-01 network profile, and add one `[MessagingParticipant]`
declaration
without changing the existing Book handlers. Everything compiles; no messaging
runtime exists yet, so no runtime fixture is added in this task.

## Required test coverage

- Missing/blank owner and publisher diagnostics.
- One contract cannot be both a message and an event.
- Every used message/event is registered exactly once in the network.
- Participant identity and subscription declarations are deterministic.
- An assembly with two `[MessagingParticipant]` declarations is diagnosed.
- Identity-less participants generate no receive queue or subscription
  declarations.
- Explicit contract protocol plus conflicting network default is diagnosed.
- Existing Rebus-only contracts continue to compile and route.
- A named consumer identity on a network without `Receive` is diagnosed naming
  the capability; a subscription or `[Event]` usage on a network without
  `PubSub` is diagnosed naming the capability.
- A `Producer`-role participant declaring subscriptions is
  diagnosed; a valid producer declaration is deterministic and owns no queue.
- Named consumer participants automatically select every network message whose
  owner queue equals the participant identity; publisher identity does not
  select events.
- Identity-less participants cannot declare subscriptions.
- Portable queue-name diagnostics cover length, case, characters, edge
  hyphens, and consecutive hyphens.
- Reserved-name diagnostics cover `outbox-processor` as participant identity,
  owner queue, or owner publisher, and `-poison`-suffixed owner queues.
- Derived event topic names exceeding the Service Bus 260-character limit are
  diagnosed.
- Current names and former-name aliases resolve deterministically.
- Default names are normalized lowercase snake_case; explicit non-normalized
  `Name`/`FormerNames` values and normalization collisions between distinct
  CLR types are diagnosed.

## Outcomes

- Contracts can declare destination queues or event publishers without Azure or
  Rebus dependencies.
- A network registers every contract, and a participant can declare identity
  and
  subscriptions at assembly scope, validated against the network's declared
  capabilities.
- Generator and runtime work from one validated metadata model.

## Acceptance

- [x] Message and event attributes are public, documented, validated, and
  covered by API-surface tests.
- [x] Network contract, participant identity, and event subscription metadata
  are
  deterministic.
- [x] Invalid ownership, duplicate, and protocol-conflict diagnostics are stable.
- [x] Existing Rebus generation and tests remain behaviorally compatible.
- [x] The [task board](../README.md) status for AZM-02 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
