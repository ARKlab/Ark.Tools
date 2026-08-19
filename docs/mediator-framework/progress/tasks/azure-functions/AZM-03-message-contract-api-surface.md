# AZM-03 — Message contract API-surface enforcement

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-02
**Scope**: API-SURFACE GENERATOR + CONTRACT COMPATIBILITY
**Design**: [Contract model — logical names and aliases](../../azure-functions-messaging-design.md#3-contract-model)

## Problem

Message and event logical names are persisted wire-contract identifiers, and
participant declarations own routing: which participant processes a message,
publishes an event, or subscribes to it determines queues and topics. Changing
a canonical `Name`, `FormerNames` set, participant membership, or participant
declaration can break queued
messages, routing, or event topics even though the CLR API still compiles. The
existing API-surface analyzer tracks HTTP, gRPC, and Rebus metadata, but it
does not yet track the transport-neutral `[Message]`/`[Event]` metadata or the
`[MessagingParticipant]`/`[MessagingNetwork]` declarations
introduced by AZM-02.

## Execution map

- **Analyzer project**: extend
  `src/mediator-framework/Ark.Tools.MediatorFramework.ApiSurface.Generators/ApiSurfaceGenerator.cs`.
- **Shared semantic model**: reuse or extract the exact logical-name and alias
  resolution used by the Mediator Framework generators. Do not independently
  reimplement default names, owner parsing, or alias normalization.
- **Generator tests**: extend
  `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`.
- **Sample baseline**: update
  `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/ArkApiSurface.txt`
  when the Book messaging contracts are added.
- **Analyzer documentation**: update `docs/analyzers.md` and the API-surface
  section of `docs/mediator-framework/design.md`.
- **Stop condition**: this task detects and snapshots contract metadata only.
  It must not implement serialization, trigger generation, routing, or Azure
  resource creation.

## Snapshot format

Emit one deterministic line for each transport-neutral message or event, each
participant, and each network:

```text
MESSAGE Books.RecalculatePrint -> name:books.recalculate_print former:-
EVENT Books.PrintCompleted -> name:books.print_completed former:books.print_finished|legacy.print_completed
PARTICIPANT BookTopology.PrintingParticipant -> network:BookMessagingNetwork identity:printing processes:books.recalculate_print publishes:- subscribes:books.print_completed serializers:json,msgpack default:json
NETWORK BookTopology.BookMessagingNetwork -> members:printing_participant|web_frontend_participant requires:receive|pubsub|scheduled_send
```

The rules are fixed:

- The type before `->` is the namespace-qualified CLR type name without
  assembly qualification.
- `name` is the resolved canonical wire name in the normalized lowercase
  snake_case form defined by AZM-02, including the AZM-02 default
  when no explicit `Name` is set.
- `former` is `-` when empty; otherwise aliases are distinct and
  ordinal-sorted, joined by `|`.
- `PARTICIPANT` lines record the resolved network identity, the resolved
  identity (explicit or normalized class-name default), and ordinal-sorted
  processes/publishes/subscribes wire names (`-` when empty), the
  ordinal-sorted serializer set, and the default serializer.
- `NETWORK` lines record ordinal-sorted member type names and the declared
  capability flags in flag-value order (`receive|pubsub|scheduled_send`,
  `-` when none).
- Lines are ordinal-sorted with all other API-surface entries.
- A type carrying other supported transport attributes keeps those existing
  entries as well; message/event entries do not replace `CONTRACT` or `REBUS`
  lines.

## Implementation steps

1. Add the fully qualified metadata names for `[Message]`, `[Event]`,
   `[MessagingParticipant]`, and `[MessagingNetwork]` to
   `ApiSurfaceGenerator`.
2. Collect attributed types through incremental
   `ForAttributeWithMetadataName` providers and combine them with the existing
   HTTP, gRPC, and Rebus contract set without duplicate symbol processing.
3. Resolve the canonical name, `FormerNames`, participant identity, network
   membership, and processes/publishes/subscribes sets through the same
   immutable
   metadata/helper used by the messaging generators. If AZM-02 initially puts
   this logic in a generator-specific class, extract a source-linked
   Roslyn-only helper usable by both generators.
4. Emit the exact `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` formats
   above. Use
   `StringComparer.Ordinal` for deduplication and ordering.
5. Extend snapshot parsing to accept only well-formed `MESSAGE`, `EVENT`,
   `PARTICIPANT`, and `NETWORK`
   prefixes in addition to the existing formats. A malformed line must still
   produce `ARKAPI004`.
6. Ensure `ContractOwner` maps a changed message/event line back to the CLR
   contract symbol and a changed participant/network line back to the
   participant/network class so `ARKAPI002` is reported at the declaration
   when possible.
7. Treat changes to the canonical name, alias set, participant identity,
   network membership, processes/publishes/subscribes sets, serializer set,
   or default serializer as API-surface
   drift. Do not classify additions to `FormerNames` as invisible merely
   because they are backward-compatible; accepting any wire-contract change
   requires a reviewed baseline diff.
8. Update the sample baseline with the Book message/event/participant/network
   entries generated by
   the analyzer. Never hand-author values that differ from emitted output.
9. Document that changing an event canonical name, its publisher, or a
   subscriber's membership also changes topics/subscriptions and
   requires the explicit topology migration defined by the messaging design;
   accepting `ARKAPI002` alone does not perform that migration.

## Guide contribution

Update `docs/mediator-framework/guide/azure-functions.md` to explain that
canonical message names, former-name aliases, participant declarations, and
network member lists are part of
`ArkApiSurface.txt`. Include the build-failure and baseline-acceptance workflow,
plus the additional event-topic migration requirement.

## Sample extension

When AZM-02 annotates the Book contracts, regenerate the Application sample's
`ArkApiSurface.txt` and retain both its existing Rebus entries and the new
transport-neutral `MESSAGE`/`EVENT`/`PARTICIPANT`/`NETWORK` entries. The
sample must demonstrate that
the same CLR contract can contribute separate Rebus and Mediator Framework
surface lines without implying wire interoperability.

## Required test coverage

- Default canonical names include the namespace, exclude assembly identity,
  and are normalized to lowercase snake_case.
- Explicit `Name` appears exactly in the generated line.
- Empty aliases and empty participant sets emit `former:-` / `publishes:-`.
- Multiple aliases are deduplicated and ordinal-sorted.
- `PARTICIPANT` lines record identity, network, ordinal-sorted
  processes/publishes/subscribes, serializer set, and default serializer.
- `NETWORK` lines record ordinal-sorted members and capability flags in
  flag-value order.
- Changing `Name`, aliases, participant identity, membership, or any
  participant set produces `ARKAPI002`.
- Matching baselines produce no API-surface diagnostic.
- Malformed `MESSAGE`, `EVENT`, `PARTICIPANT`, or `NETWORK` lines produce
  `ARKAPI004`.
- A contract carrying `[RebusMessage]` plus `[Message]` retains both entries.
- Repeated compilations produce byte-for-byte identical output.

## Outcomes

- Persisted message identities and routing ownership (participant
  declarations and network membership) are visible in committed
  API-surface diffs.
- Accidental message-name, alias, ownership, membership, and event-topic
  changes fail the build.
- The API-surface analyzer and messaging generators cannot drift in contract
  identity resolution.

## Acceptance

- [ ] `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` entries follow the
  fixed deterministic format.
- [ ] Canonical names, ownership, membership, and `FormerNames` changes
  trigger `ARKAPI002`.
- [ ] Snapshot parsing and contract-local diagnostics cover the new entries.
- [ ] The Book sample baseline contains generated transport-neutral entries.
- [ ] Analyzer and Mediator Framework guides document baseline acceptance and
  event-topic migration.
- [ ] The [task board](../README.md) status for AZM-03 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
