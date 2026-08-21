# AZM-03 — Message contract API-surface enforcement

**Category**: azure-functions-messaging · **Priority**: foundation
**Depends on**: AZM-02, AZM-03A
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
- **Shared semantic model**: reuse the source-linked logical-name, alias, and
  identity resolution helper owned by
  `Ark.Tools.MediatorFramework.Generators` (AZM-03A). Do not independently
  reimplement default names, owner parsing, or alias normalization.
- **Generated-member exclusion**: honor the AZM-03A API-surface exclusion
  marker attribute so the generated partial-class routing members never
  appear in `ArkApiSurface.txt`; routing drift is tracked solely by the
  `MESSAGE`/`EVENT`/`PARTICIPANT`/`NETWORK` lines below.
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
MESSAGE Books.RecalculatePrint -> name:books_recalculate_print former:-
EVENT Books.PrintCompleted -> name:books_print_completed former:books_print_finished|legacy_print_completed
PARTICIPANT BookTopology.PrintingParticipant -> network:BookMessagingNetwork identity:printing processes:books_recalculate_print publishes:- subscribes:books_print_completed serializers:json,msgpack default:json
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
   metadata/helper used by the messaging generators — the source-linked
   Roslyn-only helper owned by `Ark.Tools.MediatorFramework.Generators`
   (AZM-03A).
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

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

*Incremental provider registration for the four messaging attributes,
mirroring the existing `ApiSurfaceGenerator` style: fully qualified
metadata-name constants (like the existing `Http`/`Grpc`/`Rebus`),
`ForAttributeWithMetadataName` + `Collect()`, a `Combine` merge, and the
existing `BuildSurface(types, cancellationToken)` which already deduplicates
by fully qualified display string and ordinal-sorts the final lines:*

```csharp
// New constants alongside the existing Http/Grpc/Rebus metadata names.
private const string Message = "Ark.MediatorFramework.MessageAttribute";
private const string Event = "Ark.MediatorFramework.EventAttribute";
private const string Participant = "Ark.MediatorFramework.MessagingParticipantAttribute";
private const string Network = "Ark.MediatorFramework.MessagingNetworkAttribute";

// Inside Initialize(IncrementalGeneratorInitializationContext context):
var messageTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
        Message,
        static (_, _) => true,
        static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
    .Collect();
var eventTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
        Event,
        static (_, _) => true,
        static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
    .Collect();
var participantTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
        Participant,
        static (_, _) => true,
        static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
    .Collect();
var networkTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
        Network,
        static (_, _) => true,
        static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
    .Collect();

// Extend the existing merge; BuildSurface groups by the fully qualified
// display string, so a type carrying [RebusMessage] plus [Message] is
// processed once and contributes both its REBUS and MESSAGE lines.
var contractTypes = httpTypes.Combine(grpcTypes).Combine(rebusTypes)
    .Combine(messageTypes).Combine(eventTypes)
    .Combine(participantTypes).Combine(networkTypes)
    .Select(static (tuple, _) =>
    {
        var ((((((http, grpc), rebus), messages), events), participants), networks) = tuple;
        return http.AddRange(grpc).AddRange(rebus)
            .AddRange(messages).AddRange(events)
            .AddRange(participants).AddRange(networks);
    });

var surfaceProvider = contractTypes.Select(static (types, cancellationToken) =>
    BuildSurface(types, cancellationToken));
```

*Exact emitted line formats (same examples as the Snapshot format section
above — these strings are the wire-drift baseline, byte-for-byte):*

```text
MESSAGE Books.RecalculatePrint -> name:books_recalculate_print former:-
EVENT Books.PrintCompleted -> name:books_print_completed former:books_print_finished|legacy_print_completed
PARTICIPANT BookTopology.PrintingParticipant -> network:BookMessagingNetwork identity:printing processes:books_recalculate_print publishes:- subscribes:books_print_completed serializers:json,msgpack default:json
NETWORK BookTopology.BookMessagingNetwork -> members:printing_participant|web_frontend_participant requires:receive|pubsub|scheduled_send
```

*Emission helper sketch, called from `AddType` beside the existing
HTTP/gRPC/Rebus emission. Canonical-name, alias, and identity resolution is
delegated to the shared source-linked helper owned by
`Ark.Tools.MediatorFramework.Generators` (AZM-03A) — never reimplemented
here. All ordering and deduplication uses `StringComparer.Ordinal`:*

```csharp
private static void AddMessagingLines(List<string> lines, INamedTypeSymbol type)
{
    // Namespace-qualified CLR type name, no assembly qualification.
    var clrName = type.ToDisplayString();

    var message = Attribute(type, Message);
    if (message is not null)
    {
        var name = SharedNameResolver.ResolveCanonicalName(type, message);
        var former = FormatSet(SharedNameResolver.ResolveFormerNames(message));
        lines.Add($"MESSAGE {clrName} -> name:{name} former:{former}");
    }

    var @event = Attribute(type, Event);
    if (@event is not null)
    {
        var name = SharedNameResolver.ResolveCanonicalName(type, @event);
        var former = FormatSet(SharedNameResolver.ResolveFormerNames(@event));
        lines.Add($"EVENT {clrName} -> name:{name} former:{former}");
    }

    // PARTICIPANT {clrName} -> network:{network} identity:{identity}
    //   processes:{set} publishes:{set} subscribes:{set}
    //   serializers:{set} default:{protocol}
    // NETWORK {clrName} -> members:{set} requires:{flags-in-value-order or -}
    // Both use the same shared resolution helper and FormatSet.
}

// Distinct, ordinal-sorted, '|'-joined; '-' when the set is empty.
private static string FormatSet(IEnumerable<string> values)
{
    var sorted = values.Distinct(StringComparer.Ordinal)
        .OrderBy(static v => v, StringComparer.Ordinal)
        .ToArray();
    return sorted.Length == 0 ? "-" : string.Join("|", sorted);
}
```

*Honoring the AZM-03A generated-member exclusion marker (conceptual name
`Ark.MediatorFramework.MessagingGeneratedSurfaceAttribute`; declared by
AZM-03A): members carrying the marker never contribute snapshot lines, so
the generated partial routing members produce no API-surface churn:*

```csharp
private const string GeneratedSurface =
    "Ark.MediatorFramework.MessagingGeneratedSurfaceAttribute";

private static bool IsGeneratedSurface(ISymbol symbol)
{
    return symbol.GetAttributes().Any(static attribute =>
        attribute.AttributeClass?.ToDisplayString() == GeneratedSurface);
}

// Applied wherever type members are walked, e.g. in AddType:
foreach (var member in AllProperties(type))
{
    if (IsGeneratedSurface(member))
        continue; // generated routing members never reach ArkApiSurface.txt
    AddContract(lines, request, member, string.Empty, visited);
}
```

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

- [x] `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` entries follow the
  fixed deterministic format.
- [x] Canonical names, ownership, membership, and `FormerNames` changes
  trigger `ARKAPI002`.
- [x] Snapshot parsing and contract-local diagnostics cover the new entries.
- [x] The Book sample baseline contains generated transport-neutral entries.
- [x] Analyzer and Mediator Framework guides document baseline acceptance and
  event-topic migration.
- [x] The [task board](../README.md) status for AZM-03 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
