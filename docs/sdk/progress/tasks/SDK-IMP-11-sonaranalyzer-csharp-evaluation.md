# SDK-IMP-11 — SonarAnalyzer.CSharp evaluation

**Status**: Pending analysis (draft) · **Category**: Analyzer evaluation  
**Depends on**: SDK-IMP-04

## Problem

Evaluate whether `SonarAnalyzer.CSharp` should be added to the Ark.Tools SDK
analyzer baseline.

## Analysis scope

- Inventory the rules provided by `SonarAnalyzer.CSharp`.
- Compare its diagnostics with the existing .NET, Banned API, Meziantou,
  Visual Studio Threading, and ErrorProne analyzers.
- Identify overlapping or duplicate diagnostics and define which analyzer
  remains authoritative.
- Define configuration, severity, opt-out, SQL exclusion, version ownership,
  and lock-file requirements.
- Validate the impact on existing repository projects and clean-consumer
  fixtures before implementation.

**Important**: Any implementation must avoid duplicate diagnostics by
disabling duplicates deliberately rather than layering equivalent rules.

## Acceptance

- [ ] The overlap analysis and recommendation are documented.
- [ ] Duplicate diagnostics have an explicit disable/ownership decision.
- [ ] A follow-up implementation scope is approved or the proposal is rejected.
