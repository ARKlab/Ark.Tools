# SDK-IMP-09 - ReferenceProject migration

**Category**: migration · **Priority**: maintenance
**Depends on**: SDK-IMP-01 through SDK-IMP-07
**Scope**: `samples/` source-build integration and
`samples/Ark.ReferenceProject/` migration
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
  import the SDK `Sdk.props` and `Sdk.targets` from the local
  `src/sdk/Ark.Tools.Sdk/Sdk/` source-build path. The imports are removed when
  the sample is ejected.
- **Nested-solution SDK activation**: in the nested solution's own
  `Directory.Build.props` (for example,
  `samples/Ark.ReferenceProject/Directory.Build.props`), keep the parent import
  first and place the commented package SDK declaration there. Ejection replaces
  the parent/source-build imports with `<Sdk Name="Ark.Tools.Sdk"
  Version="6.6.6" />` using the released package version. Do not place this
  declaration in the shared `samples/Directory.Build.props` harness.
- **ReferenceProject cleanup**: remove SDK-owned properties, items, package
  references, package versions, analyzer configuration, global usings, content
  handling, SDK-owned test defaults, Reqnroll defaults, packaging defaults, and
  SourceLink defaults from:
  `samples/Ark.ReferenceProject/Directory.Build.props`,
  `samples/Ark.ReferenceProject/Directory.Build.targets`, and every nested
  `*.csproj`.
- **Ownership boundary**: retain target frameworks, project-specific versions
  and packability, explicit test/assertion/Reqnroll dependencies, project
  identity, project references, sample infrastructure, and all other
  consumer-owned choices.
- **Locks**: refresh affected lock files.

## Implementation steps

1. Capture a pre-migration evaluated property, item, and package-graph
   baseline for the ReferenceProject.
2. Add the `samples/Directory.Build.props` source-build switch and unconditional
  source-build SDK props/targets imports.
3. Keep the package SDK declaration at the nested solution's own
  `Directory.Build.props`; do not add it to the shared samples harness.
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
8. Confirm preserved behavior and intentional SDK-provided behavior in the
  resulting project files and validation output.

## Required test coverage

- A clean source checkout activates the local SDK without a published preview
  package.
- Each nested solution activates `Ark.Tools.Sdk` exactly once after ejection.
- Source-build SDK props and targets are imported unconditionally by the shared
  samples harness.
- Evaluated properties, items, and package graphs preserve consumer-owned
  behavior and identify intentional SDK-owned differences.
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
- Consumer-owned project and package choices remain explicit.
- No SDK preview publication is required.

## Acceptance

- [x] The source-build SDK switch and unconditional imports are present in
  `samples/Directory.Build.props`.
- [x] The nested solution `Directory.Build.props` contains the eject-time
  package SDK declaration; the shared `samples/Directory.Build.props` does not.
- [x] No duplicated SDK-owned policy remains in the ReferenceProject build
  props, build targets, nested project files, or package-version declarations.
- [x] Framework, assertion, identity, targeting, and sample infrastructure
  stay consumer-owned.
- [x] Lock files, build, tests, and packable sample outputs are updated and
  validated.
- [x] The [task board](README.md) status for SDK-IMP-09 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
