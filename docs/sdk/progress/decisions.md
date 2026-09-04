# Ark.Tools SDK — decision log

Status: **design decisions complete; ready for implementation planning**.

## How to review

- Accepted decisions are recorded in
  [`../design.md`](../design.md#accepted-decisions); reopen one only with a new
  constraint.
- Use this log as implementation input; no design decision remains open.

## Decision status

| ID | Status | Answer |
| --- | --- | --- |
| SDK-01 | DECIDED | A+C — hybrid `Ark.Tools.Sdk` and `Ark.Tools.Build` package. |
| SDK-02 | DECIDED | A — additional SDK. |
| SDK-03 | DECIDED | B — ARK-owned line-of-business repositories. |
| SDK-04 | DECIDED | A — strong defaults, every feature disableable/overridable. |
| SDK-05 | DECIDED | A — framework-neutral MTP; no framework, assertion, or VSTest package. |
| SDK-06 | DECIDED | C — explicit test selection, suffix fallback during migration. |
| SDK-07 | DECIDED | A — SDK-owned exact package versions. |
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
| SDK-19 | DECIDED | A — SBOM and Polyfill are baseline features; SourceLink is provided by the .NET SDK. |
| SDK-20 | DECIDED | A — include every current analyzer. |
| SDK-21 | DECIDED | B — SDK bans plus consumer `AdditionalFiles` and suppressions. |
| SDK-22 | DECIDED | A — build-breaking policy changes follow semantic versioning. |
| SDK-23 | DECIDED | B — migrate ReferenceProject category by category. |
| SDK-24 | DECIDED | A — public transitive policy, limited to sane defaults. |
| SDK-25 | DECIDED | A — narrow public safety baseline with project opt-outs. |

## SDK-01 — Distribution model

**Status:** DECIDED — hybrid A+C.

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
6. project-type-selected SBOM or Polyfill dependencies;
7. SDK-owned implicit versions marked `IsImplicitlyDefined`; and
8. early defaults or validation for restore inputs.

It **can** still provide analyzer/global configuration, banned-symbol
`AdditionalFiles`, global usings, non-restore properties, content metadata,
SponsorLink removal, package validation targets, SQL and Reqnroll properties,
MTP command-line properties, and test-build analyzer suppression.

### Accepted hybrid

- `Ark.Tools.Build` contains the sane, public subset of non-restore properties,
  targets, analyzer configuration, and additional files that can operate from
  `buildTransitive`.
- `Ark.Tools.Sdk` contains conditional package-reference injection and other
  SDK-only behavior.
- `Ark.Tools.Sdk/Sdk.props` injects an exact, implicit `PackageReference` to the
  matching `Ark.Tools.Build` version. An ordinary dependency in the SDK nuspec
  is insufficient: it is restored for SDK resolution but does not enter the
  consumer assets file or activate `buildTransitive`.
- A downstream project can inherit `Ark.Tools.Build` through a project or
  package dependency even if it omitted the SDK. A referenced project and an
  isolated project cannot inherit policy backwards; this remains a safety net,
  not a replacement for SDK activation.
- Runtime Ark.Tools packages do not directly depend on `Ark.Tools.Build`; only
  the SDK injects it.

The feasibility spike is recorded in
[`../design.md`](../design.md#propagation-limits).

## SDK-02 — Activation and project-SDK composition

**Status:** DECIDED — A.

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

The launch compatibility matrix is `Microsoft.NET.Sdk`,
`Microsoft.NET.Sdk.Web`, `Microsoft.NET.Sdk.Razor`, and `Microsoft.Build.Sql`
2.2.0. Additional primary SDKs can be added after a concrete consumer requires
them.

## SDK-05 — MTP baseline and framework ownership

**Status:** DECIDED — A.

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

### Resolved package boundary

The current setup also adds `MSTest.TestAdapter`, `MSTest.TestFramework`,
`MSTest.Analyzers`, `Microsoft.NET.Test.Sdk`, and `AwesomeAssertions`. A pure
MTP profile injects none of them. Like Meziantou's MTP-only SDK, Ark does not
add `Microsoft.NET.Test.Sdk`.

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

The SDK adds the MTP extensions and settings only. It does not add
`Microsoft.NET.Test.Sdk`, set `EnableMSTestRunner`, choose a test framework, or
choose an assertion package. Consumer `global.json` remains consumer-owned and
selects `"runner": "Microsoft.Testing.Platform"`.

## SDK-07 — Analyzer/tool version ownership

**Status:** DECIDED — A.

**Question:** Who owns versions for analyzers, MTP extensions, SBOM, and
Polyfill?

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

Consumers remove matching entries from `Directory.Packages.props`. Implicit
dependencies remain visible in `packages.lock.json`; SDK upgrades require
lock-file updates.

## SDK-24 — Transitive publication boundary

**Status:** DECIDED — A, with a deliberately limited public baseline.

### Verified constraint

For the forgotten-project safety net to work, the injected `Ark.Tools.Build`
reference must allow its `buildTransitive` assets to flow. The same metadata
also causes `dotnet pack` to write `Ark.Tools.Build` as a dependency. That means
an ARK library package can impose the build policy on external package
consumers. Setting `PrivateAssets="all"` prevents the packed dependency, but it
also prevents propagation to a downstream project that omitted the SDK.

Neither option protects a referenced project that omitted the SDK, because
NuGet dependency assets flow from dependencies to consumers, never backwards.
An isolated project also receives nothing.

`Ark.Tools.Build` remains a public dependency of packed libraries. Its
`buildTransitive` assets therefore reach downstream package consumers. The
package is restricted by the SDK-25 safety criteria and every included default
has an override or escape hatch.

SDK activation is still validated because transitive flow is directional:
referenced and isolated projects do not inherit policy from their consumers.

## SDK-25 — `Ark.Tools.Build` baseline cross-check

**Status:** DECIDED — A.

The complete selected classification and rationale are in
[`../design.md`](../design.md#selected-public-transitive-baseline).

### Selected `Ark.Tools.Build`

- Set-when-empty for non-SQL C# projects: `Nullable=enable`,
  `ImplicitUsings=enable`, `GenerateDocumentationFile=true`,
  `Features=strict`, `ReportAnalyzer=true`, and
  `EnforceCodeStyleInBuild=true`.
- Set-when-empty for all projects: `TreatWarningsAsErrors=true` and
  `MSBuildTreatWarningsAsErrors=true`.
- Set-when-empty only when `UsingMicrosoftBuildSqlSdk=true`:
  `TreatTSqlWarningsAsErrors=true` and `RunSqlCodeAnalysis=true`.
- Separately packaged configuration assets:
  `Ark.Tools.CodingStyle.editorconfig`,
  `Ark.Tools.NetAnalyzers.globalconfig`,
  `Ark.Tools.MeziantouAnalyzer.globalconfig`,
  `Ark.Tools.ErrorProne.globalconfig`,
  `Ark.Tools.VisualStudioThreading.globalconfig`,
  `Ark.Tools.IdentityModel.globalconfig`,
  `Ark.Tools.Core.globalconfig`, and
  `BannedSymbols.Ark.txt`.
- Removal of only the `DevLooped.SponsorLink` and `Moq.CodeAnalysis` analyzers.
- No dependencies, project-type inference, test/output/publish/pack behavior,
  global usings, restore policy, or environment workaround.

### Selected `Ark.Tools.Sdk`

- Restore/CI policy, `AnalysisLevel=latest-all`, `LangVersion=14.0`, and all
  exact implicit package references.
- Visual Studio acceleration for validated primary SDKs, package-backed
  settings, project classification, test and MTP behavior, Reqnroll
  properties/content, application/test settings files, packaging behavior, the
  three Ark global usings, and the Copilot SourceLink workaround.

### Selected exclusions

- TFMs/versions, global packability, warning suppressions, unsafe blocks,
  assembly/organization identity, icon, Application Insights dummy resource,
  exact dependency rewriting, static-graph workaround, local Ark.Tools.Core
  interceptor wiring, sample project-reference replacement, and properties
  already defaulted appropriately by the .NET SDK.

### Solution alternatives

- **A — Narrow safety baseline above.** Public compiler safety, analyzer
  configuration, SQL properties behind explicit SQL capability, and no
  topology or restore changes.
- **B — Configuration only.** Move every property to the SDK; Build contains
  analyzer configuration, banned symbols, local overrides, and SponsorLink
  removal only. Forgotten-SDK projects lose the compiler safety defaults.
- **C — Broad non-restore baseline.** Also move content, test, packaging, global
  using, and native-default properties into Build. This maximizes forgotten-SDK
  coverage but changes unknown external consumers and violates the selected
  “sane defaults” constraint.

### Decision

**A.** Keep the narrow classification. It gives a forgotten-SDK consumer the
compiler safety and analyzer configuration baseline without selecting
dependencies, inferring project roles, changing output/pack topology, or
overriding defaults already maintained by the .NET SDK.

XML documentation and standard implicit usings remain public defaults. A
project can opt out directly with `GenerateDocumentationFile=false` or
`ImplicitUsings=disable`; project properties evaluate after NuGet package props
and therefore override these set-when-empty defaults.

## Accepted implementation details

These details refine the accepted decisions and remain subject to executable
tests:

- **Overrides:** properties use `Condition="'$(Property)' == ''"`. Named
  `EnableArkTools*` switches suppress Build assets. When the SDK is active, the
  same switch also removes the corresponding implicit package.
- **EditorConfig:** package files are included directly as
  `EditorConfigFiles`, matching Meziantou.NET.Sdk. Every analyzer configuration
  is a separate named package file, including `IDX00001` and `ARKCORE005`
  severities split from coding style. Analyzer/global settings participate in
  build and design-time Roslyn analysis. A source-tree `.editorconfig` wins over
  global config entries; deeper source-tree EditorConfig files win over
  shallower ones. Neither package writes consumer files.
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
- **Packaging:** include symbols and validation. Exclude
  organization identity, license, icon, repository URL, and the exact
  project-reference dependency rewrite.
- **Baseline packages:** SBOM, Polyfill, .NET analyzers, Banned API, Meziantou,
  VS Threading, and ErrorProne are independently disableable.
- **Version ownership:** the SDK injects exact, implicit versions; matching
  consumer CPM entries are removed and SDK updates refresh lock files.
- **Banned symbols:** SDK and consumer lists compose. Consumers suppress an
  individual diagnostic with justification rather than editing the SDK file.
- **Compatibility:** newly enforced diagnostics, bans, mandatory properties,
  and implicit-package major upgrades require an SDK major release.
- **Migration:** remove copied ReferenceProject settings one category at a time
  and compare evaluated properties/items plus build results at every step.
