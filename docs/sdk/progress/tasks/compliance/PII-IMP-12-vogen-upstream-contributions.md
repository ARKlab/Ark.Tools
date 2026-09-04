# PII-IMP-12 — Upstream Vogen contributions

**Category**: compliance-upstream · **Priority**: low
**Depends on**: PII-IMP-04
**Scope**: EXTERNAL CONTRIBUTION (no Ark.Tools code)
**Design**: [Contributing to Vogen instead](../../../privacy-by-default-prd.md#143-contributing-to-vogen-instead),
[Interop, not exclusion](../../../privacy-by-default-prd.md#145-interop-not-exclusion)

## Problem

Ark does not build on Vogen (§14.2), but Vogen types classified with Ark
attributes are a supported combination, and two of the traps `ARKPII010` reports
have no user-side switch. Both gaps are generic improvements that benefit every
Vogen user, so they belong upstream rather than in a workaround.

## Execution map

- **`DebuggerAttributeGeneration.None`** — Vogen emits `[DebuggerDisplay("… {
  _value }")]` and a `DebuggerTypeProxy` unconditionally, so a Vogen value object
  cannot hide its value in a debugger or in any tooling that reads those
  attributes.
- **`Conversions.Protobuf`** — protobuf-net has no Vogen support at all; the
  documented answer is a hand-written surrogate per type.
- Both are proposed as ordinary feature requests with tests, in Vogen's own
  style, and neither is a prerequisite for any other PII-IMP task.
- If either is declined, `ARKPII010` keeps reporting the trap with the manual
  workaround in its message; nothing in the Ark design changes.

## Analysis scope

- Confirm both gaps still exist on Vogen `main` before opening anything.
- Open one issue per feature, then one PR per accepted issue.
- Record the outcome in [`../../decisions.md`](../../decisions.md) so the reason
  `ARKPII010` still reports the trap stays traceable.

## Acceptance

- [ ] Both gaps re-verified against current Vogen `main`.
- [ ] An issue is filed for each, referencing the concrete use case.
- [ ] A PR is opened for each accepted issue, or the refusal is recorded.
- [ ] The outcome is reflected in `ARKPII010`'s message and in `decisions.md`.
- [ ] The [task board](../README.md) status for PII-IMP-12 matches this task.
