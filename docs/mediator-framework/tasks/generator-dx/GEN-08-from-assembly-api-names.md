# GEN-08 — Name assembly-scanning APIs explicitly

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

The generated registration APIs take a generic marker type, but their names do not explain why:

- `MapArkEndpoints<TAssemblyMarker>`
- `MapArkGrpcServices<TAssemblyMarker>`
- `RegisterArkRebusHandlers<TAssemblyMarker>`

The marker is not a mapped contract, service, or handler. Its assembly is the generator's scan scope.
At call sites such as `MapArkEndpoints<RefreshGreetingCommand>()`, the current names make that contract
easy to misunderstand and make future overloads ambiguous.

## Design

Rename the APIs to make the assembly-selection semantics explicit:

- `MapArkEndpointsFromAssembly<TAssemblyMarker>`
- `MapArkGrpcServicesFromAssembly<TAssemblyMarker>`
- `RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>`

Keep signatures and behavior otherwise unchanged. The generic parameter remains a marker because it
provides compile-time assembly selection without reflection strings. This is a pre-release API, so
remove the old names rather than generating obsolete aliases and carrying duplicate surface area.
`ConfigureArkRebusRouting<TAssemblyMarker>` is outside this task because its name already describes a
different operation; assess and document it separately if its marker semantics need the same naming
rule.

## Steps

1. Update Minimal API, gRPC, and Rebus generator call-site discovery to recognize only the new names,
   including syntax-provider filters and assembly-name extraction.
2. Rename the emitted methods and update their XML summaries to state that `TAssemblyMarker` selects
   the assembly scanned for attributed contracts/handlers.
3. Update generator snapshots and tests to assert the new declarations, discovery behavior, and
   absence of the old method names.
4. Update all sample and test call sites, package descriptions, design/migration documentation, and
   examples. Search source and tracked generated fixtures for stale names.
5. Add or retain a cross-assembly test proving a marker in an application/contracts assembly causes
   that assembly to be scanned while the hosting assembly remains the generated-code owner.

## Outcomes

- All generic registration APIs state at the call site that the marker selects a scanning assembly.
- No legacy ambiguous registration names remain in the pre-release API or documentation.

## Acceptance

- [ ] Generated APIs are named `MapArkEndpointsFromAssembly<TAssemblyMarker>`,
      `MapArkGrpcServicesFromAssembly<TAssemblyMarker>`, and
      `RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>`.
- [ ] Generator call-site discovery and assembly selection work under the new names for all three
      transports.
- [ ] Cross-assembly generator/behavior tests prove the marker assembly, not the host assembly or
      marker type itself, controls discovery.
- [ ] No source, package description, test fixture, sample, or mediator-framework document references
      the three old method names except migration/history text that explicitly identifies them as old.
- [ ] No obsolete forwarding aliases are emitted.
- [ ] Full solution build + tests green.
