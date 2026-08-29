# SDK-IMP-08 — Compatibility matrix and paired-package release gate

**Category**: validation · **Priority**: release
**Depends on**: SDK-IMP-05, SDK-IMP-06, SDK-IMP-07
**Scope**: INTEGRATION TESTS + PACKAGE VALIDATION + CI
**Design**: [Compatibility and validation requirements](../../design.md#compatibility-and-validation-requirements)

## Problem

Feature-level tests do not prove SDK import order, public NuGet propagation, or
the negative boundary across supported primary SDKs and target frameworks. The
two packages also must never publish with mismatched versions or missing assets.

## Execution map

- **Primary SDK matrix**: validate `Microsoft.NET.Sdk`,
  `Microsoft.NET.Sdk.Web`, `Microsoft.NET.Sdk.Razor`, and
  `Microsoft.Build.Sql` 2.2.0.
- **Framework matrix**: validate .NET 8 and .NET 10 single-target projects plus
  a `net8.0;net10.0` multi-target project.
- **Restore matrix**: use empty caches, CPM on/off, lock files, local/CI modes,
  and consumer overrides.
- **Propagation graph**: cover direct Build reference, SDK injection,
  downstream `ProjectReference`, downstream packed `PackageReference`,
  referenced/upstream project, and isolated project.
- **Negative controls**: prove an SDK nuspec dependency alone does not activate
  Build, a transitive-only consumer receives no SDK-only behavior, and native
  .NET SDK defaults remain native.
- **Configuration**: prove packaged versus local analyzer precedence through
  command-line and design-time builds; record one Visual Studio and one Rider
  smoke result without making either IDE a test dependency.
- **Publication**: update `.github/workflows/ci.yml` and
  `.github/workflows/publish_nuget.yml` so both projects are included,
  mismatched package versions are rejected, both nupkgs are inspected, and the
  validated matching preview pair is published to the configured Ark package
  source.
- **Breaking contract**: encode package checks for the accepted public
  properties, switches, configuration paths, and exact implicit dependencies.
  Newly enforced diagnostics/bans, mandatory properties, and implicit-package
  major upgrades require an SDK major version. Compare against the latest
  published pair and record the classification/version decision in
  `docs/sdk/progress/release-review.md`; the first release records the baseline.

## Implementation steps

1. Consolidate all clean-consumer scenarios into a data-driven compatibility
   matrix in `tests/Ark.Tools.Sdk.Tests`; do not create a second harness.
2. Restore each scenario against only the test-local feed plus explicitly
   configured upstream sources.
3. Build and pack a dependency graph that proves downstream-only Build flow and
   inspects the resulting assets files and nuspec dependencies.
4. Snapshot the exact Build-only evaluated properties/items and separately the
   complete SDK profile; fail on leakage in either direction.
5. Add design-time build scenarios for configuration inputs and SDK import
   ordering; record manual IDE smoke evidence in the task Validation section
   when executed.
6. Add paired-version and package-content validation to the existing CI and
   NuGet publication workflows without introducing an ad hoc release script.
   Produce the release-review comparison against the latest published pair.
7. Run a release dry run that packs, inspects, installs, builds, tests, and
   packs a clean consumer using the produced pair.
8. After approval and green gates, publish the matching preview pair used by
   SDK-IMP-09.

## Required test coverage

- First restore succeeds with an empty package cache and versionless SDK
  resolution.
- All primary SDK and target-framework combinations build with correct
  properties/items and no duplicate imports.
- CPM consumers without duplicate SDK-owned versions restore; prohibited
  duplicates fail as designed.
- Build flows downstream through project and package dependencies and is
  emitted publicly in packed dependencies.
- Build does not flow backwards to referenced projects or sideways to isolated
  projects.
- A transitive-only consumer receives exactly the public Build baseline, no
  package injection, test/content/pack topology, or Ark global usings.
- Every local override and feature switch works at its documented import point.
- Package contents, nuspec dependency metadata, and embedded exact Build version
  match the same release version.
- Release review records the previous-version comparison and semantic-version
  decision.
- Native `DebugType`, `DebugSymbols`, `Deterministic`,
  `EmbedUntrackedSources`, and `EnableNETAnalyzers` compatibility fixtures pass.

## Outcomes

- Supported project types and framework combinations have executable proof.
- The public propagation and non-propagation boundaries are stable.
- CI can produce a matched preview pair safe for ReferenceProject migration.

## Acceptance

- [ ] The complete primary-SDK, target-framework, restore, and propagation
  matrix passes from clean caches.
- [ ] Build-only and SDK-only snapshots prove the exact boundary.
- [ ] Command-line, design-time, Visual Studio, and Rider evidence is recorded.
- [ ] Existing CI/package publication rejects mismatched or malformed package
  pairs.
- [ ] Release review approves the semantic version and a matching preview pair
  is available from the Ark package source.
- [ ] A release dry run installs and uses only the produced nupkgs.
- [ ] The [task board](README.md) status for SDK-IMP-08 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
