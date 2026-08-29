# Ark.Tools SDK — decision log

Status: **second feedback round requested**.

## How to review

- Reply to each `OPEN` or `PARTIALLY DECIDED` item with the option letter and
  amendments.
- Accepted decisions are recorded in
  [`../design.md`](../design.md#accepted-decisions); reopen one only with a new
  constraint.
- Implementation remains blocked by SDK-01, SDK-02, SDK-05, and SDK-07.

## Decision status

| ID | Status | First-round answer |
| --- | --- | --- |
| SDK-01 | OPEN | Need a precise list of `buildTransitive` limitations. |
| SDK-02 | OPEN | No answer received. |
| SDK-03 | DECIDED | B — ARK-owned line-of-business repositories. |
| SDK-04 | DECIDED | A — strong defaults, every feature disableable/overridable. |
| SDK-05 | PARTIALLY DECIDED | MTP and extensions are expected; do not add Reqnroll or AwesomeAssertions packages. |
| SDK-06 | DECIDED | C — explicit test selection, suffix fallback during migration. |
| SDK-07 | OPEN | No answer received. |
| SDK-08 | DECIDED | Packaged config, local overrides, no consumer-file changes. |
| SDK-09 | DECIDED | A — warnings as errors always by default; consumer can override. |
| SDK-10 | DECIDED | B — remove all current blanket `NoWarn` entries. |
| SDK-11 | DECIDED | Strict features; pin C# 14; consumer can override; exclude unsafe. |
| SDK-12 | DECIDED | A — lock files for every project and locked CI restore. |
| SDK-13 | DECIDED | A — full MTP extension/default-settings profile. |
| SDK-14 | DECIDED | A — optimize `dotnet test` by disabling analyzers by default. |
| SDK-15 | DECIDED | A — preserve current application-settings behavior for all projects. |
| SDK-16 | DECIDED | A — Reqnroll properties on tests, inert when Reqnroll is absent. |
| SDK-17 | DECIDED | A — automatic SQL detection and SQL-specific behavior. |
| SDK-18 | DECIDED | B — safe packaging defaults only; no organization profile. |
| SDK-19 | DECIDED | A — SBOM, SourceLink, and Polyfill are baseline features. |
| SDK-20 | DECIDED | A — include every current analyzer. |
| SDK-21 | DECIDED | B — SDK bans plus consumer `AdditionalFiles` and suppressions. |
| SDK-22 | DECIDED | A — build-breaking policy changes follow semantic versioning. |
| SDK-23 | DECIDED | B — migrate ReferenceProject category by category. |

## SDK-01 — Distribution model

**Status:** OPEN.

**Question:** Is the product an MSBuild SDK or a NuGet package family using
`buildTransitive`?

### What one `buildTransitive` package cannot do

NuGet excludes restore-affecting properties/items contributed by package build
assets. In particular it cannot add, remove, or alter `PackageReference`,
`PackageVersion`, `PackageDownload`, or target-framework inputs. That prevents a
single package from implementing:

1. non-SQL-only analyzer package references;
2. an independently disableable ErrorProne analyzer dependency;
3. test-only MTP extension dependencies;
4. CI-selected GitHub versus Azure DevOps report extensions;
5. conditionally selected test-framework/assertion packages;
6. project-type-selected SourceLink, SBOM, or Polyfill dependencies;
7. SDK-owned implicit versions marked `IsImplicitlyDefined`; and
8. early defaults or validation for restore inputs.

It **can** still provide analyzer/global configuration, banned-symbol
`AdditionalFiles`, global usings, non-restore properties, content metadata,
SponsorLink removal, package validation targets, SQL and Reqnroll properties,
MTP command-line properties, and test-build analyzer suppression.

### Solution alternatives

- **A — One additive MSBuild SDK.** It conditionally injects all required
  packages. Every project references the SDK. Version is pinned in
  `global.json`.
- **B — One `buildTransitive` package plus explicit consumer package
  references.** The standard carries configuration and targets; every consumer
  separately declares analyzers, MTP extensions, SourceLink, SBOM, and Polyfill.
  Feature opt-outs remove configuration but cannot remove nuspec dependencies.
- **C — A `buildTransitive` package family.** At minimum:
  `Ark.Tools.Build`, `.Analyzers`, `.Test`, and optional ErrorProne,
  SourceLink/SBOM/Polyfill packages. Consumers compose the required references.
- **D — SDK plus compatibility package.** Two supported activation/import paths
  with equivalent-behavior and duplicate-import tests.

### Recommendation

**A.** It is the only single-artifact solution that preserves conditional
dependencies and per-feature escape hatches. C is viable if CPM-native
`PackageReference` adoption is more important than one artifact. B does not
fully standardize setup; D doubles the support matrix before a compatibility
need exists.

### Requested answer

- A, B, C, or D.
- If B or C: may dependency selection remain explicit in every consumer
  project?
- If C: is the minimum four-package family acceptable?
- Must a runtime Ark.Tools package ever pull build policy transitively? The
  recommendation is no.

## SDK-02 — Activation and project-SDK composition

**Status:** OPEN.

**Question:** If SDK-01 selects an SDK, should Ark compose with or wrap primary
project SDKs?

### Solution alternatives

- **A — Additional SDK.**

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <Sdk Name="Ark.Tools.Sdk" />
  </Project>
  ```

  One Ark package composes with .NET, Web, Razor, Microsoft.Build.Sql, and
  third-party SDKs. It must detect project type from evaluated properties.

- **B — Wrapper SDK family.**

  ```xml
  <Project Sdk="Ark.Tools.Sdk.Web" />
  ```

  Ark publishes Base, Web, Razor, Test, and SQL wrappers. Each wrapper imports
  its Microsoft SDK and shared Ark logic. Project intent is explicit, but every
  upstream SDK/version/import-order combination becomes Ark's responsibility.

- **C — Hybrid.** One additional base SDK plus dedicated Test and SQL SDKs.
  Specialized projects become explicit while ordinary library/web projects use
  composition.

### Import-order consequences

- An additional top-level `Sdk` element gets implicit props/targets imports, but
  its position relative to `Directory.Build.props`, the primary SDK, and custom
  SDK hooks must be tested.
- A wrapper controls imports and can set early defaults before the primary SDK,
  but Ark must track every wrapped SDK and cannot transparently support an
  unknown third-party primary SDK.
- Microsoft.Build.Sql 2.2.0 is the current SQL fixture. Initial compatibility
  must also cover `Microsoft.NET.Sdk`, `.Web`, and `.Razor`, plus the existing
  .NET 8/.NET 10 multi-target matrix.

### Recommendation

**A.** One additional SDK minimizes packages and preserves arbitrary primary
SDK choice. Move to C only if an executable import-order test proves Test or SQL
cannot be safely selected from evaluated properties.

### Requested answer

- A, B, or C.
- Confirm the launch matrix:
  `Microsoft.NET.Sdk`, `Microsoft.NET.Sdk.Web`, `Microsoft.NET.Sdk.Razor`, and
  `Microsoft.Build.Sql` 2.2.0.
- Are WindowsDesktop, Worker, Blazor WebAssembly, Android/iOS, or another
  third-party SDK required at launch?

## SDK-05 — MTP baseline and framework ownership

**Status:** PARTIALLY DECIDED.

### Accepted constraints

- MTP is the standard test platform.
- Test projects receive the current extension set: CrashDump, CodeCoverage,
  HangDump, HotReload, Retry, TrxReport, and AzureDevOpsReport.
- SDK-13 selected their current default settings, and every extension/default
  has an escape hatch.
- The SDK does not add Reqnroll or AwesomeAssertions packages.
- Test projects still receive:
  `ReqnrollUseIntermediateOutputPathForCodeBehind=true` and
  `ReqnrollDeleteObsoleteCodeBehindFilesOnClean=true`. Reqnroll consumes these
  only when its build targets are present.
- The current shared build has no AwesomeAssertions-specific property. With no
  package reference there is therefore no inert AwesomeAssertions setting to
  preserve.

### Remaining package boundary

The current setup also adds `MSTest.TestAdapter`, `MSTest.TestFramework`,
`MSTest.Analyzers`, `Microsoft.NET.Test.Sdk`, and `AwesomeAssertions`. A pure
MTP profile should not inject the first three or AwesomeAssertions. The open
point is `Microsoft.NET.Test.Sdk`: Meziantou's MTP-only SDK deliberately never
adds it, while Ark currently does.

### Solution alternatives

- **A — Framework-neutral MTP.** Add MTP extensions and settings only. Projects
  explicitly reference their MTP-capable framework, Reqnroll adapter, and
  assertions. Do not add `Microsoft.NET.Test.Sdk` or set
  `EnableMSTestRunner`.
- **B — MTP plus compatibility bridge.** Same as A, but retain
  `Microsoft.NET.Test.Sdk` and `EnableMSTestRunner=true` until all Ark fixtures
  pass without them; remove them in the next major SDK version.
- **C — Framework auto-detection.** Add framework packages after inspecting
  existing references. This needs conditional restore inputs and silently
  expands project dependencies.

### Recommendation

**A**, validated with one plain MSTest project and one Reqnroll.MsTest project
using `global.json` runner selection. Properties can configure packages already
chosen by the consumer; the SDK should not choose the framework.

### Requested answer

- A, B, or C.
- Should the SDK fail with guidance when a test project has no MTP-capable test
  framework, or allow an intentionally empty test executable?
- Confirm `global.json` remains consumer-owned but must select
  `"runner": "Microsoft.Testing.Platform"`.

## SDK-07 — Analyzer/tool version ownership

**Status:** OPEN.

**Question:** Who owns versions for analyzers, MTP extensions, SourceLink, SBOM,
and Polyfill?

### Constraint

SDK-19 and SDK-20 require these packages by default. Their versions and
configuration are tested together. CPM raises `NU1009` if a consumer declares a
`PackageVersion` for the same package as an SDK-injected reference marked
`IsImplicitlyDefined=true`.

### Solution alternatives

- **A — SDK-owned.** Every injected reference contains an exact version,
  `PrivateAssets=all` where appropriate, and `IsImplicitlyDefined=true`.
  Consumers remove matching CPM entries and upgrade the SDK to upgrade tools.
- **B — Consumer-owned CPM.** The SDK injects versionless references and
  requires every repository to provide matching `PackageVersion` items. This
  retains central visibility but permits untested analyzer/config combinations.
- **C — SDK-owned defaults with version override properties.** Each package has
  an Ark property for its version. This supports exceptions but creates a large
  compatibility matrix.
- **D — Consumer references.** The SDK validates presence and gives guidance;
  consumers declare all packages and versions explicitly.

### Recommendation

**A.** Configuration plus analyzer/tool versions are one product. A feature
escape hatch disables its implicit reference; a version exception should
normally use another SDK release rather than an untested property.

### Requested answer

- A, B, C, or D.
- If A: accept removing matching entries from consumer
  `Directory.Packages.props`.
- Confirm implicit dependencies remain visible in `packages.lock.json` and SDK
  upgrades require lock-file updates.

## Accepted implementation details

These details refine the accepted decisions and remain subject to executable
tests:

- **Overrides:** properties use `Condition="'$(Property)' == ''"`. Package
  features use named `EnableArkSdk*` switches that remove the package,
  configuration, and targets together.
- **EditorConfig:** package files are included directly as
  `EditorConfigFiles`, matching Meziantou.NET.Sdk. Analyzer/global settings
  participate in build and design-time Roslyn analysis. A source-tree
  `.editorconfig` wins over global config entries; deeper source-tree
  EditorConfig files win over shallower ones. The SDK writes no files.
- **Warnings:** `TreatWarningsAsErrors`,
  `MSBuildTreatWarningsAsErrors`, and `EnforceCodeStyleInBuild` default to
  `true` in every configuration but remain overrideable. The SDK adds none of
  `NU1701`, `CS1591`, `CS1998`, or `NU1605` to `NoWarn`.
- **Language:** `Features=strict` and `LangVersion=14.0` are defaults;
  `AllowUnsafeBlocks` is absent.
- **MTP defaults:** the full extension set is referenced for tests. Defaults
  enable TRX, coverage, crash/hang dumps, hot reload, retry, Azure DevOps
  report, and minimum-test protection. The implementation must document each
  switch, dump timeout/type, and CI artifact responsibility.
- **Test optimization:** `RunAnalyzers=false` before `_MTPBuild` by default.
  `OptimizeTestRun=false` restores analyzer execution.
- **Content:** existing `appsettings*.json`, `reqnroll*.json`, and
  `testconfig.json` metadata is retained. Reqnroll items/properties are limited
  to test projects; unmatched globs and absent Reqnroll targets are inert.
- **SQL:** `UsingMicrosoftBuildSqlSdk=true` selects
  `TreatTSqlWarningsAsErrors` and `RunSqlCodeAnalysis`, and excludes C#-specific
  analyzer/configuration items.
- **Packaging:** include SourceLink, symbols, and validation. Exclude
  organization identity, license, icon, repository URL, and the exact
  project-reference dependency rewrite.
- **Baseline packages:** SBOM, SourceLink, Polyfill, .NET analyzers, Banned API,
  Meziantou, VS Threading, and ErrorProne are independently disableable.
- **Banned symbols:** SDK and consumer lists compose. Consumers suppress an
  individual diagnostic with justification rather than editing the SDK file.
- **Compatibility:** newly enforced diagnostics, bans, mandatory properties,
  and implicit-package major upgrades require an SDK major release.
- **Migration:** remove copied ReferenceProject settings one category at a time
  and compare evaluated properties/items plus build results at every step.
