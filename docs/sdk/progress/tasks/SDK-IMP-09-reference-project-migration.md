# SDK-IMP-09 - ReferenceProject migration

**Category**: migration · **Priority**: maintenance
**Depends on**: SDK-IMP-01 through SDK-IMP-07
**Scope**: `samples/` source-build integration and
`samples/Ark.ReferenceProject/` migration evidence
**Design**: [Accepted decisions](../../design.md#accepted-decisions),
[Current feature inventory](../../design.md#current-arktools-feature-inventory)

## Problem

ReferenceProject currently carries policy that is supplied by Ark.Tools SDK and
Build assets. It must consume the SDK through the same source-build arrangement
used by the other sample packages, then remove only the duplicated SDK-owned
configuration while preserving consumer-owned behavior.

SDK-IMP-09 must not depend on a published preview package or publish one as part
of the migration.

## Execution map

- **Source-build switch**: in `samples/Directory.Build.props`, set
  `_ArkToolsSdkSourceBuild` to `true`.
- **Common SDK imports**: after the existing parent build props are evaluated,
  conditionally import the SDK `Sdk.props` and `Sdk.targets` from the local
  `src/sdk/Ark.Tools.Sdk/Sdk/` source-build path. Keep the imports conditional
  on `_ArkToolsSdkSourceBuild` and the existing Ark.Tools build opt-out.
- **Per-sample SDK activation**: in each applicable
  `samples/{xxx}/Directory.Build.props`, import the parent `Directory.Build.props`
  first, then add `Ark.Tools.Sdk` conditionally. Follow the same parent-import
  structure used by the existing `samples/{xxx}/Directory.Build.targets`.
- **ReferenceProject cleanup**: remove SDK-owned properties, items, package
  references, package versions, analyzer configuration, global usings, content
  handling, test/MTP defaults, Reqnroll defaults, packaging defaults, and
  SourceLink defaults from:
  `samples/Ark.ReferenceProject/Directory.Build.props`,
  `samples/Ark.ReferenceProject/Directory.Build.targets`, and every nested
  `*.csproj`.
- **Ownership boundary**: retain target frameworks, project-specific versions
  and packability, explicit test/assertion/Reqnroll dependencies, project
  identity, project references, sample infrastructure, and all other
  consumer-owned choices.
- **Locks and evidence**: refresh affected lock files and record only
  intentional before/after differences in
  `docs/sdk/progress/reference-project-migration.md`.

## Implementation steps

1. Capture a pre-migration evaluated property, item, and package-graph
   baseline for the ReferenceProject.
2. Add the `samples/Directory.Build.props` source-build switch and conditional
   SDK props/targets imports.
3. Update each applicable sample `Directory.Build.props` to import its parent
   first and conditionally add `Ark.Tools.Sdk`.
4. Remove SDK-owned definitions from the ReferenceProject
   `Directory.Build.props`, `Directory.Build.targets`, nested project files,
   and package-version declarations.
5. Restore, evaluate, build, and test after each cleanup category. Resolve
   newly exposed diagnostics with code changes or narrow consumer overrides;
   do not restore blanket suppressions.
6. Verify the existing sample target imports still provide sample-development
   infrastructure and that the SDK is activated exactly once per project.
7. Refresh lock files and validate locked restore, sample build, full tests, and
   packable sample outputs.
8. Update migration evidence with preserved behavior, intentional SDK-provided
   behavior, and any accepted differences.

## Required test coverage

- A clean source checkout activates the local SDK without a published preview
  package.
- Each applicable sample activates `Ark.Tools.Sdk` exactly once.
- SDK props and targets are imported only under the source-build condition.
- Evaluated before/after snapshots preserve consumer-owned behavior and
  identify each intentional SDK-owned difference.
- Analyzer configurations and bans load once; local sample overrides still win.
- MTP discovers all sample tests with consumer-owned MSTest and Reqnroll
  packages.
- Appsettings, Reqnroll, and testconfig output/publish behavior is unchanged.
- SQL projects receive SQL policy and no C# analyzer profile.
- Locked restore, sample build, full tests, and packable sample validation pass.
- No copied SDK-owned setting, item, or package version remains in the
  ReferenceProject build props, build targets, nested project files, or package
  version declarations.

## Outcomes

- ReferenceProject becomes an executable source-build adoption example rather
  than a copied policy template.
- Migration evidence distinguishes intentional SDK-provided behavior from
  regressions.
- Consumer-owned project and package choices remain explicit.
- No SDK preview publication is required.

## Acceptance

- [ ] The source-build SDK switch and conditional imports are present in
  `samples/Directory.Build.props`.
- [ ] Each applicable sample `Directory.Build.props` imports its parent first
  and conditionally adds `Ark.Tools.Sdk`.
- [ ] No duplicated SDK-owned policy remains in the ReferenceProject build
  props, build targets, nested project files, or package-version declarations.
- [ ] Framework, assertion, identity, targeting, and sample infrastructure
  stay consumer-owned.
- [ ] Lock files, build, tests, and packable sample outputs are updated and
  validated.
- [ ] Migration evidence records preserved behavior and intentional differences.
- [ ] The [task board](README.md) status for SDK-IMP-09 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
