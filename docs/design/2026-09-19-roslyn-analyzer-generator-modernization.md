# Roslyn Analyzer and Generator Modernization

## Status

Approved design. Implementation requires a separate approved plan.

## Goals

- Preserve existing diagnostics, generated output, runtime behavior, and public APIs.
- Improve IDE responsiveness by keeping incremental pipelines cacheable and minimizing semantic work.
- Make deterministic output independent of declaration order and host line endings.
- Reduce confirmed allocations in analyzer and generated-code hot paths.
- Improve maintainability only where an affected generator needs a clearer pipeline, parser, spec, or emitter boundary.

## Scope

The work covers the Roslyn analyzers and generators in Core, Compliance, and MediatorFramework. It only changes pipelines with a confirmed improvement; it does not perform a repository-wide file-layout conversion.

## Architecture

### Incremental generators

Changed complex generators use explicit pipeline stages:

1. Cheap syntax or attribute shape filter.
2. Semantic parsing into minimal, immutable, value-equatable specifications.
3. Collection and composition of specifications only after projection.
4. Deterministic emission from specifications, without semantic-model access.

Specifications must not retain symbols, syntax nodes, locations, semantic models, or mutable collections across incremental boundaries. Location information required for diagnostics is reduced to stable source data and recreated only for reporting.

Attribute discovery only targets type declarations when the attribute contract requires a type. Invocation pipelines first reject nonmatching syntax shapes before semantic binding.

### Deterministic source generation

Affected emitters order declarations and generated artifacts by ordinal stable keys. Hint names are injective or include a stable identity-derived suffix. Generated source uses LF line endings regardless of the build host.

### Analyzer hot paths

Compilation-wide analyzer callbacks perform one interface traversal per type when checking multiple related interfaces. Recursive taint and lexicon matching paths use direct iteration rather than allocation-heavy LINQ projections. Compilation-relative dates are captured once at compilation start.

## Component Changes

### Core

- Reuse one generated object-value buffer per intercepted row sequence.
- Materialize the interceptor type model once after combining fields and properties.
- Collapse `SelfGenericInterfaceAnalyzer` interface checks into one traversal.
- Add cache tracking only for the modified interceptor pipeline.

### Compliance

- Project compliance-surface usage and member data to equatable specifications before aggregation.
- Move invocation registration discovery from compilation-wide semantic walking to a filtered syntax provider.
- Reuse SQL-policy classification precedence data.
- Replace declaration lexicon and recursive sink-flow allocation hotspots with direct loops.
- Capture sink-review date once per compilation and propagate cancellation through test-data scanning.
- Add field-policy regression coverage and cache tests only for modified generator pipelines.

### MediatorFramework

- Use type declaration predicates and defensive projections for attribute-based generators.
- Apply cheap invocation-shape checks before semantic binding for HTTP, gRPC, and Rebus mappings.
- Refactor changed aggregate generators to parse equatable specifications before collection and emit only from specifications.
- Use lookup dictionaries for repeated gRPC and Azure Functions duplicate/reachability checks.
- Bound messaging-network discovery to the cardinality needed and observe cancellation.
- Correct MCP multi-marker discovery; make documentation inputs tracked and generated context/hint-name handling valid for supported type shapes.
- Add named pipeline tracking and unrelated-edit cache regression tests only for modified pipelines.

## Error Handling and Compatibility

Invalid or transient source must not cause generators to throw. Unsupported source shapes either preserve the current diagnostic behavior or are ignored safely where no diagnostic contract exists. All existing diagnostic IDs, severities, locations, and message contracts remain stable unless a test identifies an existing location defect caused by the changed pipeline.

## Testing

- Preserve and run focused existing analyzer and generator tests.
- Add behavioral regressions for each changed correctness or allocation path.
- Add incremental driver tests for changed pipelines: unrelated edits keep named stages cached or unchanged; relevant edits mark them modified.
- Verify generated text is deterministic across input ordering and contains LF-only line endings where changed.
- Build affected projects, inspect emitted generated files for changed source generators, and run repository security validation before completion.

## Non-Goals

- Changing analyzer rule semantics or public APIs.
- Refactoring every generator into role-specific files.
- Adding dependencies.
- Adding cache tests for unchanged pipelines.
