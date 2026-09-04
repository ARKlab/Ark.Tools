# SDK-IMP-10 — SDK capabilities documentation

**Category**: documentation · **Priority**: release
**Depends on**: SDK-IMP-08, SDK-IMP-09
**Scope**: SDK CAPABILITY REFERENCE + PACKAGE README + RELEASE REVIEW
**Design**: [Whole design](../../design.md),
[Decision log](../decisions.md)

## Problem

Consumers need a concise reference for the capabilities provided by the SDK and
the properties it sets. The reference must make defaults, conditions, evaluation
timing, and overrides visible without reproducing implementation details.

## Execution map

- **Capability overview**: describe the SDK capabilities in the same compact
  style as the Meziantou.NET.Sdk README.
- **Property reference**: provide one table covering every public property the
  SDK sets or exposes, with its default or condition, evaluation timing, and
  direct override.
- **Capability details**: link the focused analyzer, MTP, content, and packaging
  references, including generated items/packages and opt-out properties where
  they are part of the capability.
- **Package README**: include a concise capability summary and a link to the
  property table without duplicating the full design.
- **Release review**: update stable SDK documentation links and scan for stale
  claims about SDK capabilities or property names.

## Implementation steps

1. Extract every public property and capability from packed props/targets and
  make the property table fail review if an implemented control is omitted.
2. Record whether each value is set when empty, conditionally, or
  unconditionally, and when it must be defined to affect evaluation.
3. Add the capability overview and focused links for analyzer, MTP, content,
  and packaging behavior.
4. Verify property names, defaults, conditions, evaluation timing, generated
  items/packages, and opt-out names against produced artifacts.
5. Complete a final design-versus-implementation inventory and record any
  deliberately deferred capability as a new task rather than silently
  omitting it.

## Required test coverage

- Every property-table row matches the packed props/targets and an existing
  automated test or fixture covers the behavior.
- Relative Markdown links resolve.
- Package READMEs and the stable documentation use the same capability and
  property names.
- Full capability/property inventory matches implementation and the accepted
  design.

## Outcomes

- Consumers can understand the SDK capabilities and override their properties
  without reading internal task documents.
- Package pages provide a concise capability summary and link to the property
  table.
- Stable design documentation reflects shipped behavior.

## Acceptance

- [x] The capability overview and complete property table document each SDK
  capability, default or condition, evaluation timing, and override.
- [x] Every documented property is covered by an existing automated test or
  fixture.
- [x] Focused capability references and package READMEs link to the property
  table.
- [x] Final design-to-package capability inventory has no unexplained gap.
- [x] The [task board](README.md) status for SDK-IMP-10 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
