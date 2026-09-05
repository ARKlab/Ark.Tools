# Standardized .NET solution setup

The stable consumer-facing capability and property reference is
[`reference.md`](reference.md). It is the concise adoption document; this
design records the rationale and full decision history.

Status: **design accepted and implemented**. See the
[stable consumer reference](reference.md) for adoption guidance.

## Problem

Ark.Tools and its samples maintain build properties, analyzer packages,
analyzer configuration, banned symbols, test infrastructure, and packaging
behavior in repository-local files. Consumers copy those files from
`samples/Ark.ReferenceProject`, after which fixes and new defaults drift.

The desired product is a versioned, centrally maintained setup that:

- applies consistent build and analysis defaults;
- carries analyzer configuration and `BannedSymbols.Ark.txt`;
- can vary package references and behavior by project type;
- remains overridable where a solution has a legitimate exception;
- supports Microsoft.Testing.Platform (MTP);
- does not copy Ark.Tools repository-only behavior into consumer solutions.

This document evaluates an MSBuild SDK and a conventional NuGet package with
`buildTransitive` assets. It does not define the implementation backlog.

## Verified platform constraints

### MSBuild SDK

An MSBuild SDK is itself distributed as a NuGet package. Its `Sdk/Sdk.props`
and `Sdk/Sdk.targets` are implicitly imported at the beginning and end of a
project. It can be referenced by:

- `Project Sdk="Ark.Tools.Sdk/version"`;
- an additional top-level `Sdk` element; or
- a versionless SDK reference whose version is pinned once under
  `msbuild-sdks` in `global.json`.

SDK props participate early enough in project evaluation to add conditional,
implicit `PackageReference` items. Such references should carry
`IsImplicitlyDefined="true"` and their versions remain SDK-owned. With Central
Package Management (CPM), consumers must not also declare `PackageVersion`
items for those implicit packages because NuGet reports `NU1009`.

### NuGet `buildTransitive` package

A conventional package can place `<package-id>.props` and
`<package-id>.targets` under `buildTransitive`. NuGet generates imports for
those files during restore and the assets can flow to projects that consume the
package transitively.

NuGet explicitly excludes properties and items that affect restore when they
come from package build assets. The prohibited examples include
`TargetFramework`, `PackageReference`, `PackageVersion`, and
`PackageDownload`. Therefore a `buildTransitive` package cannot reliably
reproduce the current conditional analyzer and test `PackageReference` items.
Its package dependencies must instead be fixed in the nuspec dependency graph,
split into multiple packages, or declared by every consumer.

This confirms the important functional difference: both choices can carry
props, targets, analyzer configuration, and additional files, but only an
MSBuild SDK can own conditional implicit package references during restore.

An SDK package's nuspec dependency is resolved only for the SDK resolver; it
does not enter the consuming project's assets file and therefore does not
activate the dependency's `buildTransitive` imports. The SDK must instead add an
exact, implicit `PackageReference` to the standards package from `Sdk.props`.
That reference participates in the consumer restore graph.

### Capabilities unavailable to `buildTransitive`

The following requested or accepted capabilities cannot be implemented by a
single `buildTransitive` package with equivalent behavior:

| Capability | Why it is unavailable | Required workaround |
| --- | --- | --- |
| Add analyzer packages only to non-SQL projects | Package build assets cannot add `PackageReference`; nuspec dependencies are unconditional. | Separate C#/SQL packages or require consumer references. |
| Remove ErrorProne from restore when independently disabled | A nuspec dependency is already present before package props evaluate. A target can suppress its analyzer asset but cannot remove the restored dependency. | Separate optional package or consumer reference. |
| Add MTP extensions only to test projects | A package dependency cannot be conditioned on consumer `IsTestProject`. | Separate test package referenced by each test project. |
| Select GitHub or Azure DevOps MTP reporting packages from detected CI | Nuspec dependencies cannot vary from consumer environment variables. | Restore both reporters everywhere or require CI-specific references. |
| Add Reqnroll, AwesomeAssertions, or another framework package only when selected | `PackageReference` cannot be injected after the package containing the props has been restored. | Explicit consumer references; inert properties remain possible. |
| Add SBOM and Polyfill according to project type | Their package references are restore inputs. | Make all unconditional dependencies, split packages, or require consumer references. |
| Give every injected package an SDK-owned implicit version | `IsImplicitlyDefined` applies to SDK-injected `PackageReference`; nuspec dependencies use ordinary NuGet dependency resolution. | Consumer CPM/nuspec versions, potentially with central transitive pinning. |
| Default or validate restore inputs such as TFM and package versions | NuGet explicitly excludes restore-affecting properties/items from package build assets. | Source files, `Directory.Build.props`, `Directory.Packages.props`, or an SDK. |
| Configure audit, lock-file generation, locked restore, or serialized restore globals | `NuGetAudit*`, `RestorePackagesWithLockFile`, `RestoreLockedMode`, and `RestoreSerializeGlobalProperties` alter restore. | Set and validate them in the SDK. |

Everything else currently under consideration remains possible in
`buildTransitive`: non-restore properties, targets, content item metadata,
global usings, packaged `EditorConfigFiles`/`GlobalAnalyzerConfigFiles`,
`AdditionalFiles`, SponsorLink removal, banned-package validation, SQL
properties, Reqnroll properties, analyzer suppression during test targets, and
application-settings copy/publish metadata. A package family could recover most
dependency selection, but consumers would then reference the correct base,
test, and optional-feature packages explicitly.

## Selected hybrid architecture

The second feedback round selected an additional MSBuild SDK plus a separate
`buildTransitive` package:

- `Ark.Tools.Build` carries the sane public subset of policy that does not alter
  restore inputs. It can flow to a downstream project that omitted the SDK.
- `Ark.Tools.Sdk` injects `Ark.Tools.Build` and owns conditional package
  references and other SDK-only behavior.
- `Ark.Tools.Sdk` and `Ark.Tools.Build` release together at the same version.
  The SDK adds an exact `Ark.Tools.Build` reference; a nuspec dependency is not
  sufficient.

### `Ark.Tools.Sdk`

```text
Ark.Tools.Sdk.nupkg
├── Sdk/
│   ├── Sdk.props
│   └── Sdk.targets
```

`Sdk.props` adds this conceptual restore input:

```xml
<PackageReference Include="Ark.Tools.Build"
                  Version="$(ArkToolsSdkVersion)"
                  Condition="'$(EnableArkToolsBuild)' != 'false'"
                  IsImplicitlyDefined="true" />
```

The production implementation must use an SDK-owned constant version rather
than deriving it from consumer CPM. The reference remains public so
`Ark.Tools.Build` flows through project and package dependencies.
`EnableArkToolsBuild=false`, set before SDK props evaluation, removes the
implicit package and its imports.

The SDK is additional to the project's primary SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
```

Its version is pinned under `msbuild-sdks` in consumer `global.json`. It
conditionally injects SDK-owned, exact, implicit references for analyzers,
SourceLink, SBOM, Polyfill, and MTP extensions. It detects test and SQL project
types and owns all behavior that changes restore inputs, including NuGet audit
and lock-file policy. Framework and assertion packages remain consumer-owned.

### `Ark.Tools.Build`

Publish the transitive standards package with explicit per-analyzer
configuration files:

```text
Ark.Tools.Build.nupkg
├── build/
│   ├── Ark.Tools.Build.props
│   └── Ark.Tools.Build.targets
├── buildTransitive/
│   ├── Ark.Tools.Build.props
│   └── Ark.Tools.Build.targets
└── configuration/
    ├── coding-style/
    │   └── Ark.Tools.CodingStyle.editorconfig
    └── analyzers/
        ├── Ark.Tools.NetAnalyzers.globalconfig
        ├── Ark.Tools.MeziantouAnalyzer.globalconfig
        ├── Ark.Tools.ErrorProne.globalconfig
        ├── Ark.Tools.VisualStudioThreading.globalconfig
        ├── Ark.Tools.IdentityModel.globalconfig
        ├── Ark.Tools.Core.globalconfig
        └── BannedSymbols.Ark.txt
```

The SDK package keeps the logical split between the thin `Sdk.props`/`Sdk.targets`
entry points and the actual library-wide policy under
`src/sdk/Ark.Tools.Sdk/common/*.props` and `*.targets`. The versioned
`Ark.Tools.Build` reference is injected at pack time by copying the common props
file into the intermediate output and replacing the `Version` attribute before
packaging.

`build` and `buildTransitive` expose the same implementation through one
canonical import, guarded by `ArkToolsBuildImported`, to prevent drift and
duplicate evaluation for direct consumers. This package contains no package
dependencies. Its complete proposed baseline is listed below.

The configuration is useful when the corresponding analyzer is built in or
already referenced; otherwise it is inert. A project reached transitively but
missing `Ark.Tools.Sdk` does not receive SDK-injected analyzer or MTP packages.

### Propagation limits

A local restore experiment with .NET SDK 10.0.100 verified:

1. `Sdk.props` can inject `Ark.Tools.Build` into the SDK-enabled project's
   restore graph.
2. Its `buildTransitive` props load in that project and in a downstream project
   that references it.
3. They do not flow backwards into a referenced project, or into an isolated
   project. Every project still needs the SDK unless it has a downstream
   dependency path carrying `Ark.Tools.Build`.
4. Leaving the reference transitive also writes `Ark.Tools.Build` as a
   dependency when the project is packed, so its deliberately limited baseline
   reaches external package consumers.
5. Making `Ark.Tools.Build` an ordinary dependency of the SDK nuspec downloads
   it only for SDK resolution; it does not place it in the project assets file
   or import its build assets.

This is a safety net, not a complete substitute for adding the SDK to every
project. SDK-presence validation remains required.

### Selected public transitive baseline

`Ark.Tools.Build` is public under SDK-24 option A. A setting belongs in it only
when all of these are true:

1. it does not add a package or alter restore inputs;
2. it is valid for an unknown downstream consumer;
3. it does not infer project type or alter executable, test, publish, or pack
   topology;
4. it is set only when empty, conditioned on an explicit capability where
   needed, and individually disableable; and
5. it remains useful or inert when the SDK and corresponding package are
   absent.

#### Included properties

A consumer overrides each property directly; `EnableArkToolsBuild=false`
disables the whole package baseline.
The whole-package switch must be set before NuGet props import, normally in
`Directory.Build.props` or as a global property; feature switches used by late
items/targets can also be set in the project.
Individual properties can be overridden in the project body because it
evaluates after NuGet package props. For example,
`GenerateDocumentationFile=false` disables XML documentation and
`ImplicitUsings=disable` disables standard implicit usings for that project.

“Non-SQL C#” means
`$(MSBuildProjectExtension) == '.csproj'` and
`$(UsingMicrosoftBuildSqlSdk) != 'true'`. Do not use `$(Language)`: evaluated
fixtures show it is empty for a current C# project and `C#` for a current SQL
project at this boundary.

| Property | Default and condition | Why transitive is acceptable |
| --- | --- | --- |
| `Nullable` | `enable` when empty for non-SQL C# | Standard compiler safety; does not select dependencies or outputs. |
| `ImplicitUsings` | `enable` when empty for non-SQL C# | Standard SDK compiler behavior; no Ark-specific namespaces are injected. |
| `TreatWarningsAsErrors` | `true` when empty | Core quality policy, explicitly consumer-overridable. |
| `MSBuildTreatWarningsAsErrors` | `true` when empty | Applies the same quality policy to MSBuild warnings. |
| `GenerateDocumentationFile` | `true` when empty for non-SQL C# | Produces compiler documentation without changing public API or dependency selection. |
| `Features` | `strict` when empty for non-SQL C# | Accepted compiler feature policy. |
| `ReportAnalyzer` | `true` when empty for non-SQL C# | Emits analyzer timing information in compiler output only. |
| `EnforceCodeStyleInBuild` | `true` when empty for non-SQL C# | Makes the packaged coding-style policy effective during builds. |
| `TreatTSqlWarningsAsErrors` | `true` when empty and `UsingMicrosoftBuildSqlSdk=true` | Activates only for an explicitly selected SQL SDK. |
| `RunSqlCodeAnalysis` | `true` when empty and `UsingMicrosoftBuildSqlSdk=true` | Activates only for an explicitly selected SQL SDK. |

The package does not repeat .NET SDK 10.0.400 defaults: `Deterministic=true`,
`EmbedUntrackedSources=true` for supported project types,
`DebugType=portable`, configuration-aware `DebugSymbols`, and
`EnableNETAnalyzers=true` for modern .NET. Executable fixtures must detect
upstream default changes.

#### Included items and targets

| Feature | Condition and escape hatch | Public behavior |
| --- | --- | --- |
| `Ark.Tools.CodingStyle.editorconfig` | Non-SQL; `EnableArkToolsCodingStyle` | Applies the accepted formatting, style, and naming baseline. |
| `Ark.Tools.NetAnalyzers.globalconfig` | Non-SQL; `EnableArkToolsNetAnalyzers` | Configures built-in and .NET analyzers if present. |
| `Ark.Tools.MeziantouAnalyzer.globalconfig` | Non-SQL; `EnableArkToolsMeziantouAnalyzer` | Inert if Meziantou.Analyzer is absent. |
| `Ark.Tools.ErrorProne.globalconfig` | Non-SQL; `EnableArkToolsErrorProne` | Inert if ErrorProne is absent. |
| `Ark.Tools.VisualStudioThreading.globalconfig` | Non-SQL; `EnableArkToolsVisualStudioThreading=true` | Opt-in VS Threading analyzer configuration; inert if analyzers are absent. |
| `Ark.Tools.IdentityModel.globalconfig` | Non-SQL; `EnableArkToolsIdentityModelConfiguration` | Inert if the IdentityModel analyzer is absent. |
| `Ark.Tools.Core.globalconfig` | Non-SQL; `EnableArkToolsCoreConfiguration` | Inert if the Ark.Tools.Core analyzer is absent. |
| `BannedSymbols.Ark.txt` | Non-SQL; `EnableArkToolsBannedApi` | Inert if Banned API Analyzers is absent; consumer lists compose. |
| SponsorLink analyzer removal | `EnableArkToolsSponsorLinkRemoval` | Removes only `DevLooped.SponsorLink` and `Moq.CodeAnalysis` before compilation. |

The full diagnostic contents remain in
[Analyzer and configuration assets](#analyzer-and-configuration-assets).

#### Kept in `Ark.Tools.Sdk`, not public transitively

| Area | Settings/behavior | Reason |
| --- | --- | --- |
| Restore | CI detection used by restore, `RestorePackagesWithLockFile`, `RestoreLockedMode`, `RestoreSerializeGlobalProperties`, and all `NuGetAudit*` properties | Package build assets cannot reliably alter the current restore; consistency requires early SDK evaluation. |
| Compiler/toolchain version | `AnalysisLevel=latest-all` and `LangVersion=14.0` | Avoids forcing evolving diagnostics or a C# version unsupported by an external consumer's SDK through a package dependency. |
| IDE optimization | `AccelerateBuildsInVisualStudio=true` | Safe only after validating the selected primary SDK's up-to-date-check inputs and outputs. |
| Package injection | `Ark.Tools.Build`, analyzers, Banned API, ErrorProne, MTP extensions, SBOM, and Polyfill references and versions | `PackageReference` is an SDK responsibility. |
| Package-backed properties | `GenerateSBOM` and `PolyUseEmbeddedAttribute` | Must be enabled and disabled with their injected package. |
| Project classification | `IsTestProject` explicit/suffix handling and SQL/non-SQL package selection | A public dependency must not infer downstream project type. |
| Test topology | `IsPackable=false`, `WarnOnPackingNonPackableProject=false`, `OutputType=Exe`, and `ExcludeByAttribute` | Changes project/test behavior rather than general build quality. |
| MTP | Extension defaults, reporting, dumps, coverage, retry, empty-run protection, and `_MTPBuild` analyzer suppression | Alters test execution and requires SDK-injected packages. |
| Reqnroll | Both code-behind properties plus `reqnroll*.json` handling | Requires the SDK's explicit test classification even though no Reqnroll package is injected. |
| Content | `appsettings*.json` and `testconfig.json` output/publish metadata | Alters consumer output and publish artifacts. |
| Packaging | `EnablePackageValidation`, symbols, and package-specific defaults | Alters packed artifacts and may require injected tools. |
| Source name resolution | `System.Diagnostics.CodeAnalysis`, `System.Globalization`, and `System.Text` global usings | Can introduce ambiguous names in an unknown downstream source tree. |
| Agent workaround | Copilot-only SourceLink disablement | Environment-specific workaround, not a public package policy. |

#### Excluded from both packages

TFMs, local versions, `IsPackable=true`, blanket `NoWarn`, unsafe blocks,
assembly identity attributes, organization metadata, the Ark icon,
`ApplicationInsightsResourceId`, exact project-reference rewriting,
Ark.Tools.Core project-reference interceptor support, sample project-reference
replacement, and defaults already
provided by the .NET SDK remain repository/project or platform owned.

### Comparison

| Dimension | `Ark.Tools.Sdk` | `Ark.Tools.Build` |
| --- | --- | --- |
| Activation | SDK reference in each project | SDK-injected `PackageReference`, then transitive package graph |
| Central version | `global.json` `msbuild-sdks` | Exact matching version owned by the SDK |
| Import points | Implicit top and bottom SDK imports | Generated NuGet props and targets |
| Conditional package references | Supported | Restore-affecting items are excluded |
| Conditional build properties/targets | Supported | Supported if they do not affect restore |
| Configuration/additional files | Supported | Supported |
| Transitive policy propagation | No | Downstream project and package consumers |
| Multiple project profiles | Restore-affecting conditions | Non-restore conditions only |
| Existing SDK composition | Additional SDK or wrappers | Native |
| Consumer project simplicity | One additional SDK | No explicit reference when injected by the SDK |
| CPM ownership of injected packages | SDK owns implicit versions | SDK owns the exact injected version |
| Responsibility | Restore inputs and project-type package selection | Static/transitive policy and configuration |

## Current recommendation

Implement the selected hybrid: one additional `Ark.Tools.Sdk`, pinned once in
`global.json`, plus its exact `Ark.Tools.Build` package reference. Do not make
Ark.Tools runtime libraries carry the policy package; only the SDK injects it.
The reference is public so packed libraries carry the deliberately limited
`Ark.Tools.Build` baseline to their consumers.

The accepted policy is strong defaults with escape hatches. Properties are set
when empty so repository/project values override them. Package-backed features
also require a named opt-out that removes both the implicit package and related
configuration or targets. A `buildTransitive` target can suppress build
behavior but cannot remove an already restored package.

## Accepted decisions

The feedback rounds established the following baseline:

- Distribution is hybrid: the sane public subset of non-restore policy in
  `Ark.Tools.Build/buildTransitive` subject to the public-baseline safety
  criteria; an additional `Ark.Tools.Sdk` injects that package and owns SDK-only
  behavior.
- The SDK composes with, rather than wraps, the primary project SDK.
- The audience is ARK-owned line-of-business repositories. The SDK has one
  policy set, not public/organization profiles.
- Every default is consumer-overridable. Every independently useful feature has
  an escape hatch; neither package silently overwrites consumer values.
- Test-project selection is explicit when present, with `.Tests` and
  `.UnitTests` suffix detection as migration fallback.
- MTP and its current extension set are expected. Test-framework and assertion
  packages, including `Microsoft.NET.Test.Sdk`, are not automatically added.
  Reqnroll properties are configured without adding Reqnroll; no
  AwesomeAssertions-specific property currently exists to configure.
- Packaged analyzer settings are added through `EditorConfigFiles` and
  `GlobalAnalyzerConfigFiles`, following Meziantou's mechanism. Local
  `.editorconfig` and `.globalconfig` files override package defaults. Neither
  package creates or changes consumer files.
- Compiler and MSBuild warnings are errors by default in every configuration,
  but consumers can override the properties.
- The SDK contributes none of the existing blanket `NoWarn` entries.
- `Features=strict` is enabled and `LangVersion` is pinned to C# 14, the current
  language version for the repository's .NET 10 SDK, when the consumer leaves
  them empty. `AllowUnsafeBlocks` is not set.
- Every project generates a NuGet lock file and CI uses locked mode.
  These restore-affecting defaults live in `Ark.Tools.Sdk`, not the transitive
  package.
- The full current MTP extension/default-settings profile is enabled for test
  projects: crash dump, code coverage, hang dump, hot reload, retry, TRX,
  Azure DevOps reporting, and empty-run protection. Each feature remains
  independently disableable.
- Analyzers are disabled during `dotnet test` by default to optimize the MTP
  build. Consumers can disable this optimization.
- Current `appsettings*.json` copy/publish behavior applies to all projects,
  with an escape hatch.
- Reqnroll settings apply to test projects but add no Reqnroll packages and are
  inert when Reqnroll is absent.
- SQL projects are automatically detected through
  `UsingMicrosoftBuildSqlSdk`; they receive T-SQL warnings-as-errors and SQL
  code analysis while C# analyzer/configuration items are excluded.
- Packaging adds safe public defaults only: symbols and package
  validation. It adds no identity, license, icon, repository URL, exact
  dependency-version rewrite, or organization profile.
- SBOM and Polyfill are baseline implicit dependencies, each with an escape
  hatch. SourceLink is provided by the supported .NET SDK.
- All current analyzers are baseline dependencies, including ErrorProne.
- Analyzer, MTP extension, SBOM, Polyfill, and `Ark.Tools.Build`
  package versions are exact and SDK-owned. Consumers remove matching CPM
  entries and update lock files when the SDK changes.
- All standard analyzer configurations are packaged separately by analyzer in
  `Ark.Tools.Build`; no wildcard placeholder represents the configuration set.
- The SDK banned-symbol list is combined with consumer `AdditionalFiles`;
  consumers use justified analyzer suppressions for exceptions.
- New errors/bans, mandatory properties, and implicit-package major upgrades
  are breaking SDK changes.
- ReferenceProject migrates category by category with evaluated-project and
  build snapshots.

## Current Ark.Tools feature inventory

The inventory below was verified against:

- `/Directory.Build.props`;
- `/Directory.Build.targets`;
- `/samples/Ark.ReferenceProject/Directory.Build.props`;
- `/samples/Ark.ReferenceProject/Directory.Build.targets`;
- the root analyzer configuration and banned-symbol files; and
- `/Directory.Packages.props` for package versions.

`Disposition` identifies the owner selected by SDK-25.

### Early properties

| Current feature/default | Current scope | Disposition |
| --- | --- | --- |
| `TargetFrameworks=net8.0;net10.0` at root; `TargetFramework=net10.0` in ReferenceProject | Repository/sample choice | Exclude. A reusable standard must not silently choose consumer TFMs. |
| Local `Version=999.9.9`; sample `Version=6.6.6` | Local package development | Exclude. Versioning remains repository-owned. |
| `ArkCoreInterceptorsEnabled=true`, compiler-visible property, and `Ark.Tools.Core.Generated` interceptor namespace | Ark.Tools.Core local project-reference support | Exclude initially. Published `Ark.Tools.Core` already carries its consumer build assets. |
| `ContinuousIntegrationBuild=true` for `TF_BUILD`, `GITHUB_ACTIONS`, or `CI`; `_IsGitHubActions=true` | All projects | SDK: set early only when detected and otherwise preserve an explicit value. |
| `IsPackable=true` at root; `false` in ReferenceProject | Conflicting repository defaults | Do not set globally; set `false` for detected test projects. |
| `Nullable=enable` | Non-SQL projects | Build: include when empty. |
| `ImplicitUsings=enable` | Non-SQL projects | Build: include when empty; do not add Ark global usings. |
| `TreatWarningsAsErrors=true` | All current projects | Build: include when empty in every configuration; consumer can override. |
| `MSBuildTreatWarningsAsErrors=true` | All current projects | Build: include when empty in every configuration; consumer can override. |
| `NoWarn=NU1701;1591;CS1998;NU1605` | All current projects | Exclude all four blanket suppressions. |
| `AllowUnsafeBlocks=true` | All current projects | Exclude. Unsafe remains a consumer/project decision. |
| `GenerateDocumentationFile=true` | All current projects | Build: include when empty for non-SQL C# projects. |
| `GenerateAssemblyConfigurationAttribute=false` | All current projects | Exclude. Assembly attribute policy remains repository-owned. |
| `GenerateAssemblyCompanyAttribute=false` | All current projects | Keep package/repository-owned unless all consumers use external assembly metadata. |
| `GenerateAssemblyProductAttribute=false` | All current projects | Keep package/repository-owned unless all consumers use external assembly metadata. |
| `EmbedUntrackedSources=true` | All current projects | Exclude as redundant for supported .NET SDK project types; retain an executable compatibility fixture. |
| `DebugType=portable` | All current projects | Exclude as the .NET SDK default; do not adopt Meziantou's `embedded` value. |
| `DebugSymbols=true` | All current projects | Exclude; use the .NET SDK's configuration-aware default. |
| `RestorePackagesWithLockFile=true` | All current projects | SDK: include early when empty for every project. |
| `RestoreLockedMode=true` on CI | All current projects | SDK: include early when empty and `ContinuousIntegrationBuild=true`. |
| `EnablePackageValidation=true` | All current projects | SDK: include when empty as a safe packaging default. |
| `RestoreUseStaticGraphEvaluation=false` | GitHub dependency-submission restores report NU1004 for valid lock files with static graph evaluation | SDK: include early when empty. |
| `RestoreSerializeGlobalProperties=true` | All current projects | SDK: include early when empty. |
| `Deterministic=true` | All current projects | Exclude as the .NET SDK default; retain an executable compatibility fixture. |
| `AccelerateBuildsInVisualStudio=true` | All current projects | SDK: include when empty only for validated primary SDKs. |
| `Features=strict` | All current projects | Build: include when empty for non-SQL C# projects. |
| `ReportAnalyzer=true` | Non-SQL projects | Build: include when empty. |
| `EnableNETAnalyzers=true` | Non-SQL projects | Exclude as the modern .NET SDK default; explicit consumer `false` remains respected. |
| `AnalysisLevel=latest-all` | Non-SQL projects | SDK: include when empty so the evolving diagnostic surface requires explicit SDK activation. |
| `LangVersion=latest` | Non-SQL projects | SDK: replace with overridable `LangVersion=14.0`; do not impose C# 14 through a transitive package on older SDKs. |
| `EnforceCodeStyleInBuild=true` | Non-SQL projects | Build: include when empty in every configuration. |
| `GenerateSBOM=true` | All current projects | SDK: pair with the independently disableable `Microsoft.Sbom.Targets` reference. |
| `PolyUseEmbeddedAttribute=true` | All current projects | SDK: pair with the independently disableable Polyfill reference. |
| `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=low` | All current projects | SDK: include early when empty. |
| `WarningsNotAsErrors += NU1901;NU1905` | All current projects | Exclude. Warnings are errors by default; consumers can add explicit exceptions. |
| `IsTestProject=true` for names ending `.Tests` or `.UnitTests` | Convention-based detection | SDK: use explicit value first and suffix detection as migration fallback. |
| Test `IsPackable=false` and `WarnOnPackingNonPackableProject=false` | Test projects | SDK: include in the test profile. |
| Test `OutputType=Exe` and `EnableMSTestRunner=true` | Test projects | SDK: keep `OutputType=Exe` for MTP; do not set framework-specific `EnableMSTestRunner`. |
| Test `ExcludeByAttribute=Obsolete,GeneratedCodeAttribute` | Test projects | SDK: include when empty as an inert test-platform setting. |
| `ReqnrollUseIntermediateOutputPathForCodeBehind=true` | Test projects | SDK: include when empty for test projects; inert without Reqnroll targets. |
| `ReqnrollDeleteObsoleteCodeBehindFilesOnClean=true` | Test projects | SDK: include when empty for test projects; inert without Reqnroll targets. |
| `TreatTSqlWarningsAsErrors=True`, `RunSqlCodeAnalysis=True` | ReferenceProject SQL projects | Build: include when empty only when `UsingMicrosoftBuildSqlSdk=true`; exclude C# analyzers/configs. |
| `EnableSourceControlManagerQueries=false`, `EnableSourceLink=false` when `COPILOT_AGENT_ACTION` is set | Copilot sandbox workaround | SDK: include while the sandbox limitation remains reproducible. |
| `ApplicationInsightsResourceId=/subscriptions/dummy` | Historical local telemetry workaround | Exclude unless a current failing scenario proves it is still required. |

### Analyzer and configuration assets

| Current asset | Current behavior | Disposition |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.400 | Private analyzer reference for non-SQL projects | SDK: exact implicit reference. |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` 4.14.0 | Private analyzer reference for non-SQL projects | SDK: exact implicit reference. |
| `Meziantou.Analyzer` 3.0.160 | Private analyzer reference for non-SQL projects | SDK: exact implicit reference. |
| `Microsoft.VisualStudio.Threading.Analyzers` 18.7.23 | Private analyzer reference for non-SQL projects | SDK: exact implicit reference. |
| `ErrorProne.NET.CoreAnalyzers` 0.1.2 | Private reference; root supports `DisableErrorProneAnalyzers=true` | SDK: exact implicit reference with an independent opt-out. |
| `.netanalyzers.globalconfig` | 97 CA/IDE severity overrides | Build: package and load as a global analyzer config. |
| `.meziantou.globalconfig` | 34 MA severity overrides | Build: package and load as a global analyzer config. |
| `.errorprone.globalconfig` | 30 EPC/ERP severity overrides | Build: package separately; inert when ErrorProne is absent. |
| `.vsthreading.globalconfig` | 23 VSTHRD severity overrides | Build: package and load as a global analyzer config. |
| `.editorconfig` | Formatting, code-style, naming rules, and three error severities | Build: split coding style/`IDE1006`, `IDX00001`, and `ARKCORE005` by analyzer provenance. Local source-tree config wins. |
| Consumer `.globalconfig` | ReferenceProject keeps a local override file | Consumer-owned; the package does not discover local files. |
| `BannedSymbols.Ark.txt` | 93 active bans: local time, ambiguous parsing/rounding/culture, reference tuples, implicit time-zone conversion, console logging, and blocking task/thread APIs | Build: package as `AdditionalFiles`; provide one opt-out and compose with consumer lists. |
| `Disable_SponsorLink` target | Removes `DevLooped.SponsorLink` and `Moq.CodeAnalysis` analyzers | Build: include with an opt-out. |

Analyzer versions shown are the versions pinned on 2026-08-29. The SDK owns
them; consumers remove matching CPM entries. SDK updates require lock-file
updates and analyzer upgrades are tested product changes.

Every standard configuration is a separate `Ark.Tools.Build` asset. The
following is the complete current rule inventory; an implementation copies the
settings, comments, and rationale from the source files rather than generating
one merged configuration.

#### `Ark.Tools.CodingStyle.editorconfig`

- Formatting: four-space indentation, spaces, CRLF, existing final-newline
  behavior, using ordering, C# spacing, indentation, newline, and wrapping
  preferences.
- Code style: all current .NET and C# style options from the root
  [`Ark.Tools.CodingStyle.editorconfig`](../../src/sdk/Ark.Tools.Build/configuration/coding-style/Ark.Tools.CodingStyle.editorconfig), including expression, pattern,
  namespace, modifier, `var`, and expression-body preferences.
- Naming: interfaces start with `I`; types and protected members use PascalCase;
  private/internal methods, fields, events, and properties use `_camelCase`.
- Built-in naming diagnostic: `IDE1006` is an error.

#### `Ark.Tools.IdentityModel.globalconfig`

- `IDX00001` is an error. The configuration is inert when its analyzer is
  absent.

#### `Ark.Tools.Core.globalconfig`

- `ARKCORE005` is an error. The configuration is inert when the Ark.Tools.Core
  analyzer is absent.

#### `Ark.Tools.NetAnalyzers.globalconfig`

| Severity | Configured diagnostics |
| --- | --- |
| Error | `CA1001`, `CA1063`, `CA1068`, `CA1069`, `CA1821`, `CA1823`, `CA1827`, `CA1836`, `CA1854`, `CA2000`, `CA2002`, `CA2245`, `CA5351` |
| Warning | `CA1018`, `CA1041`, `CA1047`, `CA1050`, `CA1051`, `CA1061`, `CA1067`, `CA1070`, `CA1304`, `CA1507`, `CA1816`, `CA1825`, `CA1828`, `CA1832`, `CA1833`, `CA1834`, `CA1835`, `CA1839`, `CA1841`, `CA1844`, `CA1845`, `CA1846`, `CA1847`, `CA1850`, `CA1853`, `CA1862`, `CA1864`, `CA1865`, `CA1866`, `CA1868`, `CA1869`, `CA1870`, `CA2011`, `CA2012`, `CA2016`, `CA2020`, `CA2201`, `CA2211`, `CA2213`, `CA2215`, `CA2219`, `CA2242`, `CA2250`, `CA2253`, `CA5350`, `CA5359`, `CA5360`, `CA5363`, `CA5364`, `CA5365`, `CA5379`, `CA5385`, `CA5397`, `IDE0005`, `IDE0161` |
| Suggestion | `CA1019`, `CA1040`, `CA1054`, `CA1056`, `CA1062`, `CA1305`, `CA1308`, `CA1309`, `CA1310`, `CA1510`, `CA1515`, `CA1810`, `CA1819`, `CA1859`, `CA2007` |
| None | `CA1000`, `CA1002`, `CA1024`, `CA1031`, `CA1033`, `CA1034`, `CA1707`, `CA1716`, `CA1720`, `CA1724`, `CA1812`, `CA1851`, `CA2227`, `IDE0160` |

#### `Ark.Tools.MeziantouAnalyzer.globalconfig`

| Severity | Configured diagnostics |
| --- | --- |
| Error | `MA0040`, `MA0042`, `MA0045`, `MA0078`, `MA0079`, `MA0133` |
| Warning | `MA0004`, `MA0028`, `MA0029`, `MA0043`, `MA0044`, `MA0050`, `MA0053`, `MA0057`, `MA0058`, `MA0059`, `MA0063`, `MA0067`, `MA0080`, `MA0102`, `MA0113`, `MA0114`, `MA0152`, `MA0160` |
| Suggestion | `MA0016`, `MA0051`, `MA0121` |
| Silent | `MA0006`, `MA0007`, `MA0048`, `MA0056` |
| None | `MA0015`, `MA0032`, `MA0049` |

#### `Ark.Tools.ErrorProne.globalconfig`

| Severity | Configured diagnostics |
| --- | --- |
| Error | `EPC17`, `EPC23`, `EPC25`, `EPC26`, `EPC27`, `EPC31`, `EPC33`, `EPC35`, `ERP022`, `ERP023` |
| Warning | `EPC11`, `EPC12`, `EPC13`, `EPC16`, `EPC20`, `EPC22`, `EPC24`, `EPC28`, `EPC32`, `EPC36`, `ERP021`, `ERP031` |
| Suggestion | `EPC14`, `EPC21`, `EPC29`, `EPC30`, `EPC34`, `EPC37` |
| None | `EPC15` in favor of `MA0004`; `EPC18` to preserve async stack traces |

#### `Ark.Tools.VisualStudioThreading.globalconfig`

| Severity | Configured diagnostics |
| --- | --- |
| Error | `VSTHRD003`, `VSTHRD100`, `VSTHRD101`, `VSTHRD110`, `VSTHRD114` |
| Warning | `VSTHRD001`, `VSTHRD002`, `VSTHRD004`, `VSTHRD010`, `VSTHRD103`, `VSTHRD105`, `VSTHRD106`, `VSTHRD107`, `VSTHRD109` |
| Suggestion | `VSTHRD011`, `VSTHRD102`, `VSTHRD104`, `VSTHRD108`, `VSTHRD112`, `VSTHRD113` |
| None | `VSTHRD012`, `VSTHRD111` in favor of `MA0004`, `VSTHRD200` |

#### `BannedSymbols.Ark.txt`

Package the complete current list, separately from the severity configurations.
Its 93 active entries ban local-time APIs, ambiguous enum parsing and rounding,
direct `CultureInfo` construction, reference tuples, implicit `DateTime` to
`DateTimeOffset` conversion, console output, `Thread.Sleep`, `Task.Wait`, and
`Task<T>.Result`. Consumer `AdditionalFiles` compose with this list.

### Late items and targets

| Current feature | Current behavior | Disposition |
| --- | --- | --- |
| `Polyfill` private package reference | Added to all root projects | SDK: baseline implicit dependency with an independent opt-out. |
| `Microsoft.Sbom.Targets` private package reference | Added to non-SQL projects | SDK: baseline implicit dependency with an independent opt-out. |
| Global usings | Adds `System.Diagnostics.CodeAnalysis`, `System.Globalization`, and `System.Text` for C# with implicit usings | SDK: include with an opt-out; never flow them publicly through Build. |
| `appsettings*.json` | Base files always copied to output/publish; environment variants copied to output but never publish | SDK: preserve for all projects, with one escape hatch. |
| `reqnroll*.json` | Always copied to test output | SDK: preserve only for detected test projects; inert when unmatched. |
| `testconfig.json` | Copied with `PreserveNewest` | SDK: include only in the test profile. |
| MTP extension package references | Crash dump, code coverage, hang dump, hot reload, retry, TRX, and Azure DevOps report | SDK: include the full set for test projects, with an opt-out per extension/default setting. |
| Test framework packages | `MSTest.TestAdapter`, `MSTest.TestFramework`, `MSTest.Analyzers`, `Microsoft.NET.Test.Sdk`, and `AwesomeAssertions` | Do not add any framework, assertion, or VSTest compatibility package. MTP remains framework-neutral. |
| Exact project-reference version target | Rewrites packed project dependencies as exact versions | Exclude. Repositories own dependency-version policy. |
| Ark icon and package metadata | Ark.Tools repository URL, project URL, MIT license, authors, copyright, symbols/snupkg | Exclude identity/license/icon/repository defaults and organization profiles; include symbols and package validation only. |
| `samples/Directory.Build.targets` | Replaces local Ark.Tools packages with project references and manually mirrors generated/analyzer assets | Exclude. This is monorepo sample-development infrastructure. |

## Meziantou research

The active `Meziantou.NET.Sdk` was reviewed at commit
[`503c46e`](https://github.com/meziantou/Meziantou.NET.Sdk/commit/503c46efbf23eef2555e7267b1c6a1e0de42a532).
Its deprecated `buildTransitive` predecessor,
`Meziantou.DotNet.CodingStandard`, was reviewed at commit
[`e8c6f91`](https://github.com/meziantou/Meziantou.DotNet.CodingStandard/commit/e8c6f914b78b014d367b08ace38f5b695acaae90).

The following list intentionally excludes xUnit- and MSTest-specific features.
MTP features remain because they are test-platform capabilities rather than a
test-framework choice.

### Relevant `Meziantou.NET.Sdk` capabilities

| Area | Verified feature/default | Ark.Tools consideration |
| --- | --- | --- |
| SDK shape | Base, Test, Web, Razor, Blazor WebAssembly, and Windows Desktop SDK packages wrap Microsoft SDKs and share common props/targets. | Start additive and single-package; split only after a real incompatibility. |
| Targeting | Defaults an omitted TFM to the maximum installed .NET TFM. | Reject: consumer target frameworks must be explicit. |
| General build | Preview-message suppression; repository URL publishing; embedded PDB; embedded untracked source; implicit usings; nullable; XML docs; static-graph restore; serialized global properties; analyzer reporting; strict features; deterministic output; latest-all analysis; unsafe blocks; latest language version; package validation; Visual Studio acceleration. | Compare individually with Ark defaults; do not import wholesale. |
| Warning policy | Warnings as errors on CI, Release, or detected AI-agent execution; code style enforced on CI/Release. | Consider CI/Release policy; broad AI-environment detection is not yet justified. |
| CI detection | Azure Pipelines, GitHub Actions, AppVeyor, GitLab, generic CI, Travis, CircleCI, AWS CodeBuild, Jenkins, Google Cloud Build, TeamCity, and JetBrains Space. | Expand Ark detection if those systems are supported. |
| NuGet security | Audit enabled for all dependencies at low severity; `NU1900`–`NU1904` become errors under strict builds. | Prefer explicit policy over Ark's current `WarningsNotAsErrors` exceptions. |
| Analyzer injection | Adds Meziantou and Banned API analyzers as private, implicit package references with SDK-owned versions. | Use the same mechanism for the Ark analyzer set. |
| Configuration | Packages per-analyzer editorconfig files, coding style, naming, compiler rules, default and Newtonsoft.Json banned symbols, and project-profile configs. | Use separate files and switches so provenance and overrides are clear. |
| Banned packages | Fails builds for selected package IDs with an opt-out per package. | Do not inherit Meziantou's list; consider an Ark-specific list only with evidence. |
| Diagnostics | Embeds banned symbols, editorconfig inputs, and selected GitHub environment values in binary logs. | Adopt config embedding; assess environment-data minimization before adopting CI metadata. |
| SponsorLink | Removes SponsorLink and Moq analyzers unless opted out. | Matches existing Ark behavior. |
| Runtime behavior | `RollForward=LatestMajor` for non-tests and automatically packs non-test executables as tools. | Reject as general defaults; both change runtime/package semantics. |
| Packaging | Finds README and third-party notices; supplies organization metadata/icon; derives versions and container tags from GitHub refs. | Potential optional pack profile; never use implicit repository-specific values. |
| Web | Auto-registers service defaults and configures GitHub Container Registry publication/tagging. | Out of the first baseline unless Ark defines corresponding runtime conventions. |
| npm | Discovers `package.json`, chooses `npm ci` in locked mode, defaults to `--ignore-scripts`, and uses OS/architecture-specific install stamps. | Useful optional profile, not a .NET-wide default. |
| File-based apps | Supports .NET 10 `#:sdk` and a dedicated config. | Defer until Ark has a file-based-app use case. |

### Relevant MTP capabilities

| Capability | Meziantou behavior | Ark.Tools consideration |
| --- | --- | --- |
| Test output | Sets test projects non-packable and executable. | Matches current Ark direction. |
| Extensions | Adds crash dump, hang dump, code coverage, hot reload, retry, TRX, and GitHub Actions report extensions. | Ark currently uses the same core set but Azure DevOps reporting; make CI reporters conditional. |
| Command line | Adds TRX, mini crash dumps, ten-minute mini hang dumps, CI coverage, and a minimum expected test count of one. | Include in the SDK-only test profile with individual escape hatches. |
| Empty-run protection | Defaults `MinimumExpectedTests=1`, with `0` disabling the explicit argument. | Include in the SDK-only test profile to prevent false-green test runs. |
| Test optimization | Disables analyzers before `_MTPBuild` unless opted out. | Include in the SDK-only test profile; CI retains a separately analyzed build. |
| CI reporting | Enables GitHub annotations/report with slow-test notices disabled. | Select the applicable reporter from detected CI in the SDK. |
| Runner selection | Requires MTP selection in `global.json`; does not add `Microsoft.NET.Test.Sdk` or VSTest settings. | Adopt: consumer owns `global.json` and framework; Ark adds no VSTest compatibility package. |

### Lessons from `Meziantou.DotNet.CodingStandard`

The predecessor packages its style/analyzer configuration and imports its
`build` props/targets again through `buildTransitive`. Its analyzer packages are
unconditional nuspec dependencies. It supports reproducible builds, analyzer
defaults, NuGet audit, global usings, packaging metadata, banned symbols,
SponsorLink removal, and VSTest analyzer suppression.

Its repository now states that it is deprecated and replaced by
`Meziantou.NET.Sdk`. The migration validates the architectural conclusion:
`buildTransitive` is adequate for static policy, while an SDK is better when
project-type-aware dependencies and broader build orchestration are required.

## Configuration layering

### Packaged configuration mechanism

Follow the mechanism used by Meziantou.NET.Sdk rather than copying files into a
consumer repository:

```xml
<ItemGroup Condition="'$(UsingMicrosoftBuildSqlSdk)' != 'true'">
  <EditorConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/coding-style/Ark.Tools.CodingStyle.editorconfig"
                     Condition="'$(EnableArkToolsCodingStyle)' != 'false'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.NetAnalyzers.globalconfig"
                             Condition="'$(EnableArkToolsNetAnalyzers)' != 'false'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.MeziantouAnalyzer.globalconfig"
                             Condition="'$(EnableArkToolsMeziantouAnalyzer)' != 'false'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.ErrorProne.globalconfig"
                             Condition="'$(EnableArkToolsErrorProne)' != 'false'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.VisualStudioThreading.globalconfig"
                             Condition="'$(EnableArkToolsVisualStudioThreading)' == 'true'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.IdentityModel.globalconfig"
                             Condition="'$(EnableArkToolsIdentityModelConfiguration)' != 'false'" />
  <GlobalAnalyzerConfigFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/Ark.Tools.Core.globalconfig"
                             Condition="'$(EnableArkToolsCoreConfiguration)' != 'false'" />
  <AdditionalFiles Include="$(MSBuildThisFileDirectory)../configuration/analyzers/BannedSymbols.Ark.txt"
                   Condition="'$(EnableArkToolsBannedApi)' != 'false'" />
</ItemGroup>
```

The package's style and analyzer options are direct compiler/design-time
inputs. Each analyzer has its own file and switch, including analyzer severities
currently embedded in the source coding-style configuration. Packaged global configs use a
`global_level` below the default level
`100` of a consumer `.globalconfig`, so an ordinary local global config wins.
Source-tree `.editorconfig` entries win over global configs, and deeper local
EditorConfig files win over shallower local files. The implementation must prove
the packaged-versus-local precedence with Roslyn build and IDE fixtures; it must
not generate, copy, or update a consumer file.

The proposed precedence, from lowest to highest, is:

1. Ark SDK defaults and packaged analyzer configs.
2. Repository `Directory.Build.props` and `Directory.Build.targets`.
3. Project properties/items.
4. Repository/project `.globalconfig` analyzer overrides.
5. Source-tree `.editorconfig` settings in their normal directory hierarchy.
6. Command-line global properties.

Implementation tests must prove this ordering. In particular, packaged analyzer
configuration must not prevent a consumer from lowering a diagnostic with a
documented local exception.

## Compatibility and validation requirements

An implementation is not complete until tests prove:

- first restore with an empty global package cache;
- versionless SDK resolution through `global.json`;
- SDK injection of the exact matching `Ark.Tools.Build` version;
- absence of `Ark.Tools.Build` imports when it is only an SDK nuspec dependency;
- downstream project-reference and package-reference propagation, plus proof
  that upstream and isolated projects are not covered;
- public `Ark.Tools.Build` dependency emission and downstream baseline
  propagation;
- evaluated-property snapshot proving the exact public baseline and absence of
  SDK-only properties/items in a transitive-only consumer;
- compatibility fixtures proving the native defaults deliberately omitted from
  `Ark.Tools.Build`;
- additional-SDK composition with `Microsoft.NET.Sdk`,
  `Microsoft.NET.Sdk.Web`, `Microsoft.NET.Sdk.Razor`, and
  `Microsoft.Build.Sql` 2.2.0;
- .NET 8 and .NET 10 single- and multi-target projects;
- CPM with no duplicate `PackageVersion` for implicit packages;
- package lock-file generation and locked CI restore;
- local override precedence and independent opt-out for every explicitly named
  packaged analyzer config;
- banned symbols report at the consumer source location;
- opt-outs remove both the analyzer package and its configuration;
- test-profile MTP discovery, reporting, dumps, coverage, and empty-run failure;
- packable and non-packable projects;
- no Ark.Tools repository URL, version, icon, or sample-only target leaks into a
  consumer package;
- Visual Studio, Rider, and command-line design/build behavior for code style;
- a migration build of `samples/Ark.ReferenceProject` after deleting copied
  settings one category at a time.

## Sources

### Ark.Tools

- [`Directory.Build.props`](../../Directory.Build.props)
- [`Directory.Build.targets`](../../Directory.Build.targets)
- [`samples/Ark.ReferenceProject/Directory.Build.props`](../../samples/Ark.ReferenceProject/Directory.Build.props)
- [`samples/Ark.ReferenceProject/Directory.Build.targets`](../../samples/Ark.ReferenceProject/Directory.Build.targets)
- [`Ark.Tools.CodingStyle.editorconfig`](../../src/sdk/Ark.Tools.Build/configuration/coding-style/Ark.Tools.CodingStyle.editorconfig)
- [`Ark.Tools.NetAnalyzers.globalconfig`](../../src/sdk/Ark.Tools.Build/configuration/analyzers/Ark.Tools.NetAnalyzers.globalconfig)
- [`Ark.Tools.MeziantouAnalyzer.globalconfig`](../../src/sdk/Ark.Tools.Build/configuration/analyzers/Ark.Tools.MeziantouAnalyzer.globalconfig)
- [`Ark.Tools.ErrorProne.globalconfig`](../../src/sdk/Ark.Tools.Build/configuration/analyzers/Ark.Tools.ErrorProne.globalconfig)
- [`Ark.Tools.VisualStudioThreading.globalconfig`](../../src/sdk/Ark.Tools.Build/configuration/analyzers/Ark.Tools.VisualStudioThreading.globalconfig)
- [`BannedSymbols.Ark.txt`](../../src/sdk/Ark.Tools.Build/configuration/analyzers/BannedSymbols.Ark.txt)

### External

- [Meziantou.NET.Sdk](https://github.com/meziantou/Meziantou.NET.Sdk/tree/503c46efbf23eef2555e7267b1c6a1e0de42a532)
- [Meziantou.NET.Sdk `Common.props`](https://github.com/meziantou/Meziantou.NET.Sdk/blob/503c46efbf23eef2555e7267b1c6a1e0de42a532/src/common/Common.props)
- [Meziantou.NET.Sdk `Common.targets`](https://github.com/meziantou/Meziantou.NET.Sdk/blob/503c46efbf23eef2555e7267b1c6a1e0de42a532/src/common/Common.targets)
- [Meziantou.NET.Sdk `Tests.targets`](https://github.com/meziantou/Meziantou.NET.Sdk/blob/503c46efbf23eef2555e7267b1c6a1e0de42a532/src/common/Tests.targets)
- [Meziantou.DotNet.CodingStandard](https://github.com/meziantou/Meziantou.DotNet.CodingStandard/tree/e8c6f914b78b014d367b08ace38f5b695acaae90)
- [Sharing coding style and Roslyn analyzers across projects](https://www.meziantou.net/sharing-coding-style-and-roslyn-analyzers-across-projects.htm)
- [Creating a custom MSBuild SDK to reduce boilerplate in .NET projects](https://www.meziantou.net/creating-a-custom-msbuild-sdk-to-reduce-boilerplate-in-dotnet-projects.htm)
- [Microsoft: reference an MSBuild project SDK](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk)
- [NuGet: MSBuild props and targets in a package](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets)
- [NuGet: PackageReference in project files](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- [Microsoft: analyzer configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
