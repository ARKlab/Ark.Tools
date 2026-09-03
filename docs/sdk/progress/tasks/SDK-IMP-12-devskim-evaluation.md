# SDK-IMP-12 — Microsoft.CST.DevSkim evaluation

**Status**: Pending analysis (draft) · **Category**: Analyzer evaluation  
**Depends on**: SDK-IMP-04

## Problem

Evaluate whether `Microsoft.CST.DevSkim` should be added to the Ark.Tools SDK
analyzer baseline.

## Analysis scope

- Confirm supported target frameworks, project types, and analyzer delivery
  model.
- Inventory the diagnostics and configuration needed for repository consumers.
- Compare coverage with the existing analyzer baseline and identify any
  duplicate or conflicting diagnostics.
- Define configuration, severity, opt-out, SQL exclusion, version ownership,
  and lock-file requirements.
- Validate the impact on existing repository projects and clean-consumer
  fixtures before implementation.

## Acceptance

- [ ] The compatibility and coverage analysis is documented.
- [ ] Duplicate or conflicting diagnostics have an explicit resolution.
- [ ] A follow-up implementation scope is approved or the proposal is rejected.
