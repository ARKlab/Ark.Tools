# AZM-17 — Logical names and native entity mapping

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-16
**Scope**: PUBLIC API + GENERATORS + TRANSPORT NAMING
**Design**: [Contract model](../../azure-functions-messaging-design.md#3-contract-model), [Resource naming](../../azure-functions-messaging-design.md#resource-naming-and-ownership)

## Problem

The common messaging model currently normalizes contract names to lowercase
snake case and participant identities to portable queue syntax. Logical names
therefore inherit restrictions from native entities. This prevents readable,
stable names with meaningful separators and makes transport constraints part of
the wire contract.

Before release, logical contract and topology names must be separated from the
physical names selected by each transport.

## Execution map

- **Public declarations**: allow lowercase logical names containing letters,
  digits, `-`, `_`, `.`, and `/` for contracts, participants, networks, topics,
  and subscriptions.
- **Shared generator model**: replace portable queue-name normalization with one
  logical-name validator and deterministic topic/subscription derivation.
- **Wire contract**: keep the complete logical contract name in
  `amf1-msg-type`; current names and `FormerNames` remain registry values.
- **Transport mapping**: add one deterministic logical-to-native entity-name
  mapping per transport. InMemory preserves logical names unchanged.
- **Generated artifacts**: manifests and trigger attributes contain the mapped
  native queue/topic/subscription names while descriptors and diagnostics retain
  logical names.
- **API surface**: snapshots record logical names, never transport-mapped names.
- **Migration**: update the Book sample and documentation before any public
  package release; no backward-compatible dual naming mode is required.

## Implementation steps

1. Define and document the common logical-name grammar. Explicit names and
   aliases must be lowercase, non-empty, contain only letters, digits, `-`,
   `_`, `.`, and `/`, and contain no empty or leading/trailing separator
   segments.
2. Update default contract, participant, and network name generation to produce
   valid readable logical names without applying transport restrictions.
3. Keep `amf1-msg-type`, routing registries, aliases, participant ownership, and
   topology metadata keyed by logical names.
4. Derive logical event topics from the publisher logical identity and current
   contract logical name. Derive subscription logical names from the subscriber
   and event topology without applying native restrictions.
5. Implement transport-specific native entity mapping:
   preserve every supported character when the complete logical name fits;
   otherwise retain a readable prefix and append a stable hash of the complete
   logical name. Apply mapping exactly once.
6. Make the hash algorithm, separator, casing, and truncation rules deterministic
   and version-stable. Include enough hash material to make accidental
   collisions impractical.
7. Diagnose final native-name collisions and logical names that cannot produce
   a valid native name within the provider limit.
8. Make generated Functions triggers and resource manifests use the same shared
   mapping implementation as runtime send, publish, and lifecycle management.
9. Keep `FormerNames` as receive-time aliases only. They do not create, rename,
   or alias topics and subscriptions; current-name or publisher changes require
   an explicit topology migration.
10. Regenerate sample baselines and inspect all affected emitted `.g.cs` files.

## Core code shapes

The common generator model owns logical validation and derivation. Transport
adapters own only native mapping. Generated descriptors carry both the logical
identity used by registries and the mapped native destination used by transport
operations; application code never computes native names.

One logical name must map to the same native entity in generated triggers,
resource reconciliation, sending, publishing, and receiving. Native mappings
must not be written into message-type headers or API-surface contract entries.

## Guide contribution

Update the messaging contract, serialization, Azure Functions, and API-surface
guides with the logical-name grammar, native mapping rules, collision behavior,
`FormerNames` boundary, and explicit topic migration requirement.

## Sample extension

Change the Book topology to demonstrate `-`, `_`, `.`, and `/` in representative
logical names. Show the resulting InMemory, Service Bus, and Storage Queue
native names and verify that received headers retain the original logical
contract name.

## Required test coverage

- Explicit valid logical names accept every supported separator.
- Uppercase, unsupported characters, empty segments, and leading/trailing
  separators produce targeted diagnostics.
- Defaults are deterministic and valid.
- `amf1-msg-type` preserves the complete logical contract name.
- InMemory preserves logical entity names unchanged.
- Service Bus and Storage Queue mappings preserve supported names and
  deterministically hash replaced or truncated names.
- Distinct logical names cannot silently map to the same native entity.
- Generated triggers, manifests, lifecycle operations, and send destinations
  use byte-for-byte identical native names.
- `FormerNames` resolve received payloads but create no topology aliases.
- Repeated generator runs and target frameworks produce identical output.

## Outcomes

- Logical messaging names are readable, stable, and transport-neutral.
- Provider restrictions are isolated to deterministic transport mapping.
- Wire headers, generated topology, and runtime routing cannot drift.

## Acceptance

- [ ] Logical names support the approved lowercase separator grammar.
- [ ] Every transport has deterministic, collision-checked native entity
  mapping.
- [ ] Wire headers and registries retain logical names.
- [ ] Generated triggers, manifests, lifecycle, and runtime transport operations
  share one mapping result.
- [ ] Sample, guides, snapshots, and generated-source inspections are updated.
- [ ] The [task board](../README.md) status for AZM-17 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
