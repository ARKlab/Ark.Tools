# SDK-IMP-09 — ReferenceProject migration

**Category**: migration · **Priority**: release
**Depends on**: SDK-IMP-08 and a published matching preview pair
**Scope**: `samples/Ark.ReferenceProject/` + MIGRATION EVIDENCE
**Design**: [Accepted decisions](../../design.md#accepted-decisions),
[Current feature inventory](../../design.md#current-arktools-feature-inventory)

## Problem

ReferenceProject is the source consumers currently copy. It must prove adoption
of the published SDK pair and remove duplicated policy without hiding accepted
behavior changes or making the repository depend on an SDK package that does
not yet exist at evaluation time.

## Execution map

- **Prerequisite**: use the same published preview version of `Ark.Tools.Sdk`
  and `Ark.Tools.Build`. Do not commit nupkgs or depend on a target that builds
  the SDK after MSBuild SDK resolution has already started.
- **Activation**: pin `Ark.Tools.Sdk` under `msbuild-sdks` in the sample
  `global.json` and add the additional SDK reference to every intended
  ReferenceProject C# and SQL project.
- **Incremental migration**: migrate and validate in this order:
  baseline properties; analyzer configuration/bans; analyzer/tool references
  and CPM versions; global usings and content; test/MTP/Reqnroll; packaging and
  SourceLink.
- **Consumer ownership**: retain target frameworks, versions, packability where
  project-specific, test framework, Reqnroll adapter, AwesomeAssertions,
  organization/package identity, sample project-reference replacement, and
  every other item classified as repository/project-owned.
- **Accepted changes**: remove blanket `NoWarn`, unsafe-by-default,
  Application Insights dummy resource, exact project-reference rewriting,
  static-graph workaround, `EnableMSTestRunner`, and SDK-owned package versions.
- **Locks**: refresh every affected `packages.lock.json` and verify locked CI
  restore.
- **Evidence**: record before/after evaluated properties/items, build/test/pack
  outcomes, and intentional differences in
  `docs/sdk/progress/reference-project-migration.md`.

## Implementation steps

1. Capture a pre-migration evaluated property/item and package-graph baseline.
2. Add SDK activation only after the matching preview pair is resolvable from
   the configured Ark source on a clean machine.
3. Remove one configuration category at a time; after each removal restore,
   evaluate, build, test, and update migration evidence.
4. Resolve newly exposed diagnostics through code changes or narrow justified
   consumer overrides. Do not restore blanket suppressions.
5. Keep framework/assertion/Reqnroll references explicit and verify Ark does not
   reintroduce them implicitly.
6. Refresh lock files, test locked restore, and pack all packable sample
   projects.
7. Delete only copied policy that is now supplied by the SDK; preserve
   sample-development infrastructure and project-specific choices.

## Required test coverage

- A clean checkout resolves the pinned SDK pair without a pre-populated cache.
- Every intended project activates the additional SDK exactly once.
- Evaluated before/after snapshots match for preserved behavior and identify
  each accepted difference.
- Analyzer configurations and bans load once; local sample overrides still win.
- MTP discovers all sample tests with consumer-owned MSTest/Reqnroll packages.
- Appsettings, reqnroll, and testconfig output/publish behavior is unchanged.
- SQL projects receive SQL policy and no C# analyzer profile.
- Locked restore, sample build, full tests, and sample package inspection pass.
- No copied SDK-owned setting or package version remains in sample
  `Directory.Build.*` or `Directory.Packages.props`.

## Outcomes

- ReferenceProject becomes an executable adoption example rather than a copied
  policy template.
- Migration evidence distinguishes intentional policy corrections from
  regressions.
- Consumer-owned project and package choices remain explicit.

## Acceptance

- [ ] A published matching preview pair is pinned and resolves from a clean
  checkout.
- [ ] Each migration category has recorded before/after evidence.
- [ ] No duplicated SDK-owned policy or package version remains.
- [ ] Framework, assertion, identity, targeting, and sample infrastructure stay
  consumer-owned.
- [ ] Lock files, build, tests, and package outputs are updated and validated.
- [ ] The [task board](README.md) status for SDK-IMP-09 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
