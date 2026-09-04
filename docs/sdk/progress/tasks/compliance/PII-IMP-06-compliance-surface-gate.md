# PII-IMP-06 — Compliance surface inventory and gate

**Category**: compliance-tooling · **Priority**: medium
**Depends on**: PII-IMP-04
**Scope**: SOURCE GENERATOR + ANALYZER RULES + CI GATE + TESTS
**Design**: [Compliance inventory](../../../privacy-by-default-prd.md#610-compliance-inventory),
[Decision PII‑04](../../../privacy-by-default-prd.md#17-decisions)

## Problem

Classification that nobody reviews decays. A committed, diffable inventory turns
"what personal data does this service hold?" from an archaeology exercise into a
file, and makes every addition a reviewed change.

## Execution map

- **`ArkComplianceSurface.txt`**, deterministic and stable-sorted, one line per
  classified member: declaring type, member, classification, purpose notes, and
  the egress targets it is serialised to.
- **Separate from `ArkApiSurface.txt`** (decision PII‑04): different audience and
  cadence, and a privacy diff must not hide inside an API diff.
- **`ARKPII020`**: the committed surface file does not match the compilation —
  new or changed classified data was not reviewed.
- **`ARKPII021`**: a member was removed from the surface while still classified,
  or its classification was weakened.
- **Workflow**: reuse the existing `ArkApiSurface.txt` verify/update MSBuild
  targets and CI step, with its own baseline file per project.

## Implementation steps

1. Implement the generator, reusing the `ApiSurfaceGenerator` determinism rules.
2. Implement the two comparison diagnostics against the committed baseline.
3. Add the `UpdateArkComplianceSurface` target mirroring the API-surface update
   flow, and document it.
4. Add the CI step next to the API-surface check.

## Required test coverage

- Byte-identical output across repeated builds and across target frameworks.
- Adding a classified member without updating the baseline fails the build with
  `ARKPII020`; the update target fixes it.
- Weakening a classification is reported by `ARKPII021`.
- Egress targets from PII-IMP-03 appear on the member's line.

## Outcomes

- A reviewable, committed record of personal data per assembly.
- Evidence usable for GDPR Article 30 records of processing.

## Acceptance

- [ ] `ArkComplianceSurface.txt` is generated deterministically and separately
  from the API surface.
- [ ] `ARKPII020/021` gate baseline drift.
- [ ] The update target and CI step exist and are documented.
- [ ] The [task board](../README.md) status for PII-IMP-06 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
