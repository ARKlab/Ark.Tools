# AZM-21 — Generic messaging declarations

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-17, AZM-19
**Scope**: PUBLIC API + GENERATORS + HOST BINDINGS
**Design**: [Participant declarations](../../azure-functions-messaging-design.md#participant-declarations), [Generated routing registry](../../azure-functions-messaging-design.md#generated-routing-registry)

## Decision

**Status**: Cancelled

**REASON**: The generic declaration model is not feasible in C#. The compiler validates the generic constraint on the declaration type before source generation emits the generated partial implementation, and the intended pattern requires the same type to be both the generic argument and the generated partial declaration. That creates a self-referential static-abstract requirement and fails at compile time with `CS0311` / constraint errors. A separate declaration type would change the design and defeat the goal of a single constrained declaration boundary, so the task is rejected rather than partially implemented.

## Problem

Messaging network, participant, and host bindings currently exchange declaration
types through unbounded `Type` values. Generators then rediscover and validate
members that they already emit. This loses compile-time constraints and leaves
invalid declaration references to analyzer diagnostics.

Before release, declaration references must use generic attributes constrained
by small static-abstract interfaces. The interfaces expose only the members the
generators and generated host integrations currently consume.

## Execution map

- **Public API**: replace non-generic network, participant, Functions host, and
  Rebus host declaration attributes with generic equivalents.
- **Declaration interfaces**: define separate network, participant, and host
  contracts containing only the generated static members currently read by
  messaging generators and host composition.
- **Generated declarations**: make attributed partial declarations implement
  their applicable interface through generated static members.
- **Unchanged inputs**: contract lists, pipeline-step lists, and participant
  retry policy remain type-valued attribute properties.
- **Generators**: consume constrained generic attribute arguments and stop
  accepting the removed non-generic declaration syntax.
- **Compatibility**: no obsolete aliases or dual syntax; the framework has not
  shipped.

## Implementation steps

1. Inventory every generated static network, participant, and host member read by
   the registry, Functions, Rebus, API-surface, and composition generators.
2. Define the minimum static-abstract interfaces from that inventory. Do not add
   speculative members or expose generator implementation details.
3. Introduce generic network, participant, Functions-host, and Rebus-host
   attributes whose declaration type arguments satisfy the applicable
   interfaces.
4. Keep `Members`, `Processes`, `Publishes`, `Subscribes`, `IncomingSteps`,
   `OutgoingSteps`, and `Retry` in their existing type-valued form.
5. Generate interface implementations into the existing partial declaration
   classes. Generic attributes do not change the partial-class requirement.
6. Validate that a generic declaration argument matches the attributed or bound
   declaration where applicable and report a targeted diagnostic otherwise.
7. Remove the corresponding non-generic attributes, generator discovery paths,
   tests, documentation, and API-surface entries.
8. Update Functions and Rebus host generators to call static interface members
   through constrained type parameters rather than rediscovering declaration
   metadata.
9. Preserve all current generated routing, descriptor, payload-sender, dispatch,
   registry, identity, and manifest behavior.
10. Migrate the Book sample and all compile fixtures, regenerate baselines, and
    inspect affected `.g.cs` files.

## Core code shapes

Final public names are selected by this task. The fixed rule is that each
interface contains exactly the generated static surface already needed by its
consumers:

- network: identity, immutable options, and contract registry access;
- participant: identity, descriptor/payload sender, and typed dispatch access;
- host: the generated host manifest/composition access used by its host adapter.

Interfaces must not duplicate contract arrays or configuration already carried
by attributes. Generated explicit static interface implementations may hide
plumbing that is not intended as a direct application API.

## Guide contribution

Update messaging contract, Azure Functions, Rebus, host composition, and
generator guides with generic declaration syntax, constraints, partial-class
requirements, generated static members, and migration from removed attributes.

## Sample extension

Migrate every Book network, participant, Functions host, and Rebus host
declaration. Keep contract, pipeline, and retry lists unchanged to demonstrate
the intended generic boundary.

## Required test coverage

- Valid generic declarations compile for networks, participants, Functions, and
  Rebus.
- Missing or incorrect static declaration contracts fail at compilation.
- Mismatched attributed/bound declaration types produce targeted diagnostics.
- Contract lists, pipeline-step lists, and retry policy remain type-valued.
- Non-generic declaration attributes no longer exist or generate output.
- Generated registry, descriptor, dispatch, manifest, and host behavior is
  unchanged.
- API-surface snapshots contain the generic public syntax.
- Generated output is deterministic across target frameworks.

## Outcomes

- Declaration references are constrained at compile time.
- Generator-consumed static surfaces are explicit and minimal.
- One generic declaration syntax exists before the first release.

## Acceptance

- [ ] Generic attributes replace all network, participant, Functions-host, and
  Rebus-host declaration attributes.
- [ ] Static declaration interfaces contain only currently consumed generated
  members.
- [ ] Contract, pipeline, and retry type-valued properties remain unchanged.
- [ ] All generators consume the constrained generic declarations.
- [ ] Removed non-generic syntax has no compatibility alias.
- [ ] Sample, guides, snapshots, and generated-source inspections are updated.
- [ ] The [task board](../README.md) status for AZM-21 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
