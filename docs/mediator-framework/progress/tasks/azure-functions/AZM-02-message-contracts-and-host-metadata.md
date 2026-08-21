# AZM-02 — Transport-neutral message contracts and participant declarations

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-01
**Scope**: FRAMEWORK API + GENERATOR MODEL
**Design**: [Contract model](../../azure-functions-messaging-design.md#3-contract-model), [Participant declarations](../../azure-functions-messaging-design.md#participant-declarations), [Participant roles](../../azure-functions-messaging-design.md#participant-roles), [Proposed API shape](../../azure-functions-messaging-design.md#proposed-api-shape)

## Problem

Rebus currently owns the only queue declaration attribute. The messaging
feature needs transport-neutral message/event metadata plus participant
declarations that state how each participant joins a network — without
coupling Application contracts to Azure SDK types and without duplicating
ownership on both contract and participant.

## Prerequisites

- Read [`azure-functions-messaging-design.md`](../../azure-functions-messaging-design.md).
- Preserve `[RebusMessage]` behavior and generated Rebus routing.

## Execution map

- **Public API**: place `[Message]`, `[Event]`, logical-name/alias members,
  the class-level `[MessagingParticipant]`, `IMessagingRetryPolicy`, and the
  serialization/compression enums in
  `Ark.Tools.MediatorFramework`. The participant declaration is not
  Functions-specific; Azure Functions is one hosting adapter over it.
- **Generator inputs**: extend the Azure Functions generator's immutable
  contract/participant models; reuse existing symbol-display and diagnostic
  helpers. Cross-participant validation reads participant declarations from
  the current compilation and referenced-assembly metadata via the network's
  `Members` list.
- **Generated artifact**: emit a deterministic metadata descriptor only; do
  not emit Service Bus trigger methods yet.
- **Compatibility**: leave Rebus consumption of the new metadata to AZM-14;
  only ensure legacy Rebus generation remains unchanged here.
- **Tests**: use compile-time generator fixtures for every diagnostic and API
  surface snapshots for every public member.
- **Analyzer release metadata**: record portable-name and ownership diagnostics
  in `AnalyzerReleases.Unshipped.md`.

## Implementation steps

1. Add separate public attributes for messages and events. Contracts are
   owner-free: `[Message]` and `[Event]` carry only the contract kind plus
   optional `Name`/`FormerNames`. A contract carries either `[Message]` or
   `[Event]`, never both; dual attribution is a diagnostic.
2. Add the class-level `[MessagingParticipant]` declaration with:
   - `Processes` — message contracts this participant receives and owns;
   - `Publishes` — event contracts this participant owns and publishes;
   - `Subscribes` — network events this participant wants copies of;
   - `Serializers` — the supported serialization protocol set for the
    contracts it processes, publishes, or subscribes to;
   - `DefaultSerializer` — the participant's write protocol; a message's wire
    protocol is the processing participant's default, an event's is the
    publisher's default. A default outside `Serializers` is a diagnostic;
   - optional `Retry` (`IMessagingRetryPolicy` type; documented framework
    default when omitted), `Compression`, and `CompressionMinimumSizeBytes`
    — participant-owned, free to diverge across members;
   - optional `Identity` — defaults to the class name minus a trailing
    `Participant` suffix, normalized to the portable queue-name convention
    (`PrintingFunctionsParticipant` → `printing-functions`). Every
    participant has an identity, including sender-only ones.
   Roles are inferred: a consumer declares `Processes`/`Subscribes`, a
   publisher declares `Publishes`, and a participant can be both; declaring
   nothing makes a sender-only participant. The attribute never lists
   handlers, host-local steps, a network reference, or any Azure/transport
   member. The Azure Functions host binding (participant reference plus
   trigger selection) is a separate assembly-level attribute in the Azure
   Functions package, consumed by the generator (AZM-10/AZM-11).
3. Define `IMessagingRetryPolicy` with `MaximumDeliveryCount` (N),
   `SecondLevelRetriesEnabled`, `MaximumHandlerDuration`, and `RetryDelay`.
   Document that entity/host max delivery is `2N` when second-level retries
   are enabled and `N` otherwise, that `RetryDelay` maps to Storage Queue
   `visibilityTimeout` only, and that Service Bus abandon is immediate.
   Validate `MaximumDeliveryCount >= 1`, and require `>= 2` when second-level
   retries are enabled so the normal handler always receives delivery 1.
4. Validate the AZM-01 `Members` lists and derive member needs from
   declarations at compile time:
   - every member must be a `[MessagingParticipant]` declaration; duplicate
    entries are diagnosed;
   - a participant belongs to exactly one network; being listed in two
    networks, or in none when contracts reference it, is diagnosed;
   - exactly one member processes a given message and exactly one member
    publishes a given event — zero (unwired) produces an information
    diagnostic, multiple produce errors;
   - every `Subscribes` entry is published by a member of the same network
    (unsatisfiable subscriptions are errors);
   - every subscriber's `Serializers` contains the publisher's
    `DefaultSerializer`;
   - member capability needs (`Processes`/`Subscribes` → `Receive`,
    `Publishes`/`Subscribes` → `PubSub`, delayed send → `ScheduledSend`)
    do not exceed the network's `Requires` — the diagnostic names the
    capability and the member;
   - the same contract declared by members of two different networks is an
    error.
   Never validate against a transport — transports are unknown at compile
   time.
5. Validate identities: the portable queue-name convention (3–50 lowercase
   ASCII letters, digits, or hyphens; alphanumeric first/last character; no
   consecutive hyphens) applies to explicit and normalized identities alike.
   Diagnose every violation, duplicate identities within a network, and
   reserved names: `outbox-processor` (explicit or produced by normalizing
   the class name) and identities ending in `-poison`. Derived event topic
   names (`<publisher-identity>-<contract-name>`) longer than the Service Bus
   260-character entity limit are diagnosed.
6. Add immutable internal models consumed by the generator and runtime
   manifest emitter, including the derived contract registry (contract →
   owning participant, wire protocol, route). Do not expose Roslyn symbols or
   Azure SDK types.
7. Add XML documentation and API-surface entries for every public member.
8. Add compatibility tests proving existing Rebus attributes and generated
   Rebus routing remain unchanged.
9. Require event contracts to implement their Mediator command contract and
   diagnose invalid event contract shapes. Handler discovery is not a
   generator concern.
10. Add optional `Name` and `FormerNames` to both contract attributes. Default
   `Name` to the namespace-qualified CLR type name without assembly version,
   normalized to lowercase snake_case: each namespace/type segment is
   lowercased and PascalCase word boundaries become underscores (for example
   `Books.PrintCompleted` becomes `books_print_completed`). Explicit `Name`
   and `FormerNames` values must already be in normalized form;
   non-normalized values are diagnosed.
11. Validate duplicate names/aliases (including normalization collisions
   between distinct CLR types), alias cycles, and aliases colliding with
   current names.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
transport-neutral owner-free message/event declarations, participant
declarations (processes/publishes/subscribes/serializers), inferred roles,
the default identity normalization, wire-protocol ownership (processing
participant's default for messages, publisher's for events), strict
subscription satisfiability, the lowercase snake_case logical-name
normalization, reserved names, and the network member list.

## Sample extension

Extend the Book sample contracts with `[Message]`/`[Event]` metadata, add the
Book participant declarations, and add the AZM-01 network declaration that
lists them in `Members`, without changing the existing Book handlers.
Everything compiles; no messaging runtime exists yet, so no runtime fixture is
added in this task.

## Required test coverage

- One contract cannot be both a message and an event.
- Exactly one processor per message and one publisher per event; zero produces
  an unwired information diagnostic, multiple produce errors.
- Unsatisfiable subscriptions are errors; satisfiable ones are deterministic.
- A subscriber missing the publisher's write protocol in its `Serializers`
  is diagnosed; a `DefaultSerializer` outside `Serializers` is diagnosed.
- A member that is not a participant declaration, a participant in two
  networks, or a network with duplicate members is diagnosed.
- The same contract declared by members of two networks is an error.
- Participant identity, processes/publishes/subscribes declarations are
  deterministic.
- Capability diagnostics name the missing capability and the member:
  `Processes`/`Subscribes` without `Receive`, `Publishes`/`Subscribes`
  without `PubSub`.
- A sender-only participant (all lists empty) is valid and owns no queue.
- Portable queue-name diagnostics cover the 3–50 character length, case,
  characters, edge hyphens, and consecutive hyphens, for explicit and
  normalized identities.
- Default identity is the normalized class name minus a trailing
  `Participant` suffix.
- Reserved-name diagnostics cover `outbox-processor` (explicit or
  normalized) and `-poison`-suffixed identities.
- Derived event topic names exceeding the Service Bus 260-character limit are
  diagnosed.
- Retry policy validation covers the second-level `N >= 2` invariant and the
  documented default policy.
- Existing Rebus-only contracts continue to compile and route.
- Current names and former-name aliases resolve deterministically.
- Default names are normalized lowercase snake_case; explicit non-normalized
  `Name`/`FormerNames` values and normalization collisions between distinct
  CLR types are diagnosed.

## Outcomes

- Contracts declare kind and logical identity only — owner-free and portable.
- Participants declare how they join the network; ownership, routing, and
  capability needs are derived and analyzer-validated.
- Generator and runtime work from one validated metadata model.

## Acceptance

- [x] Message, event, and participant attributes are public, documented,
  validated, and covered by API-surface tests.
- [x] Network membership, participant identity, ownership, and subscription
  metadata are deterministic.
- [x] Ownership, satisfiability, serializer-compatibility, and capability
  diagnostics are stable.
- [x] Existing Rebus generation and tests remain behaviorally compatible.
- [x] The [task board](../README.md) status for AZM-02 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
