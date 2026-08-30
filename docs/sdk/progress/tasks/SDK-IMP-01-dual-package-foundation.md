# SDK-IMP-01 — Dual-package and clean-consumer test foundation

**Category**: foundation · **Priority**: foundation
**Depends on**: accepted SDK-01, SDK-02, SDK-07, and SDK-24 decisions
**Scope**: PACKAGING + TEST INFRASTRUCTURE
**Design**: [Selected hybrid architecture](../../design.md#selected-hybrid-architecture),
[Propagation limits](../../design.md#propagation-limits)

## Problem

The implementation needs two packages with different import semantics and a
repeatable way to test them outside the Ark.Tools repository build. An SDK
nuspec dependency does not activate another package's `buildTransitive` assets,
and stale global-package-cache content can hide packaging defects.

## Execution map

- **Build package**: create
  `src/sdk/Ark.Tools.Build/Ark.Tools.Build.csproj` as a package-only project with
  no runtime assembly and no package dependencies.
- **SDK package**: create
  `src/sdk/Ark.Tools.Sdk/Ark.Tools.Sdk.csproj` with packed `Sdk/Sdk.props` and
  `Sdk/Sdk.targets`; it is additional to, not a wrapper around, a primary SDK.
- **Canonical imports**: package `Ark.Tools.Build` entry points under both
  `build/` and `buildTransitive/`; both import one canonical implementation
  guarded by `ArkToolsBuildImported`.
- **Version coupling**: generate the packed `Sdk.props` with a literal exact
  version of `Ark.Tools.Build` equal to the `Ark.Tools.Sdk` package version.
  Inject it as a public, implicit `PackageReference` only when
  `EnableArkToolsBuild != 'false'`; do not rely on a nuspec dependency or
  consumer CPM.
- **Solution**: add both projects under `/src/sdk/` in `Ark.Tools.slnx`.
- **Test project**: create `tests/Ark.Tools.Sdk.Tests`, using only test
  dependencies already centrally managed, and add it to the solution.
- **Fixture**: pack both projects into a test-local feed, create consumers under
  the test output directory, and isolate `NUGET_PACKAGES`, HTTP cache, and NuGet
  configuration for every scenario.
- **Stop condition**: do not add policy properties, analyzers, content behavior,
  test behavior, or packaging defaults in this task.

## Implementation steps

1. Scaffold the two package projects and the single test project using existing
   repository project conventions.
2. Exclude package-project build outputs and pack only the intended MSBuild
   assets.
3. Add canonical Build props/targets with the duplicate-import guard.
4. Add minimal SDK props/targets and pack-time exact Build-version generation.
5. Add a fixture that runs `dotnet restore`, `dotnet msbuild`, `dotnet build`,
   and `dotnet pack` against isolated consumers without shell helper scripts.
6. Add fixture templates for a primary `Microsoft.NET.Sdk` project, an
   additional `<Sdk Name="Ark.Tools.Sdk" />`, a local `NuGet.Config`, and
   versionless SDK resolution through `global.json` `msbuild-sdks`.
7. Inspect both `.nupkg` archives and generated nuspecs in tests.

## Required test coverage

- Both packages restore from an empty isolated global-packages folder.
- `Ark.Tools.Sdk` resolves versionlessly through the fixture's `global.json`.
- SDK activation injects exactly the matching `Ark.Tools.Build` version with
  `IsImplicitlyDefined=true`.
- `EnableArkToolsBuild=false` prevents the implicit Build reference and imports.
- `Ark.Tools.Build` is public in the consumer assets file and packed nuspec.
- A control SDK package with only a nuspec dependency does not import Build
  assets.
- Direct Build consumption does not evaluate the canonical implementation
  twice when both `build` and `buildTransitive` entries are visible.
- Package archives contain only the intended paths and no compiled placeholder
  assembly.

## Outcomes

- The repository builds a version-matched public Build package and additional
  SDK package.
- Every later task can validate real packed artifacts in a clean consumer.
- No current Ark.Tools project depends on or activates the new SDK yet.

## Acceptance

- [x] Both package projects and the shared test project are in `Ark.Tools.slnx`.
- [x] Clean-consumer restore proves versionless SDK resolution and exact Build
  injection.
- [x] Package inspection proves the Build dependency is public and
  `Ark.Tools.Build` itself has no dependencies.
- [x] The canonical Build implementation evaluates once for direct and
  transitive imports.
- [x] No existing repository project activates `Ark.Tools.Sdk`.
- [x] The [task board](README.md) status for SDK-IMP-01 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
