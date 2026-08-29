# Standardized .NET solution setup

Status: **proposed; blocked by the decisions in
[`progress/decisions.md`](progress/decisions.md)**.

## Problem

Ark.Tools and its samples maintain build properties, analyzer packages,
analyzer configuration, banned symbols, test infrastructure, and packaging
behavior in repository-local files. Consumers copy those files from
`samples/Ark.ReferenceProject`, after which fixes and new defaults drift.

The desired product is a versioned, centrally maintained setup that:

- applies consistent build and analysis defaults;
- carries analyzer configuration and `BannedSymbols.txt`;
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

## Solution alternatives

### Alternative A — additive MSBuild SDK

Publish one `Ark.Tools.Sdk` package with this conceptual content:

```text
Ark.Tools.Sdk.nupkg
├── Sdk/
│   ├── Sdk.props
│   └── Sdk.targets
├── build/
│   ├── Ark.Tools.Sdk.props
│   └── Ark.Tools.Sdk.targets
└── configuration/
    ├── Ark.Tools.editorconfig
    ├── Ark.Tools.*.globalconfig
    └── BannedSymbols.txt
```

The SDK is additional to the project's Microsoft SDK. This avoids immediately
creating wrappers for `Microsoft.NET.Sdk`, `.Web`, `.Razor`, and specialized
SDKs. `Sdk.props` owns early properties and implicit package references;
`Sdk.targets` owns build targets and late items. Project-type conditions select
test, web, SQL, and pack behavior.

The `build` assets are optional compatibility assets, not a second activation
model. They would only be included if a concrete tool requires the package to
also expose build assets.

**Advantages**

- Can add analyzer, SourceLink, SBOM, Polyfill, and MTP packages conditionally.
- Can set early defaults and compose with existing Microsoft or third-party
  project SDKs.
- One version in `global.json` can govern all projects in a solution.
- Can expose explicit opt-out properties and project-type profiles.
- Package references can be marked private and implicit.
- Closely matches the actively maintained `Meziantou.NET.Sdk` architecture.

**Disadvantages**

- Every project must activate the SDK; it does not flow through project or
  package references.
- SDK versions are not managed by `Directory.Packages.props`.
- Import ordering and composition with non-Microsoft SDKs require tests.
- IDEs and restore must resolve the SDK before the project can be fully loaded.
- A single additive SDK needs careful conditions to avoid inappropriate
  behavior in SQL, generated, and other non-C# projects.

### Alternative B — NuGet package with `buildTransitive`

Publish `Ark.Tools.Build.Standard` as a development dependency containing:

```text
Ark.Tools.Build.Standard.nupkg
├── build/
│   ├── Ark.Tools.Build.Standard.props
│   └── Ark.Tools.Build.Standard.targets
├── buildTransitive/
│   ├── Ark.Tools.Build.Standard.props
│   └── Ark.Tools.Build.Standard.targets
└── configuration/
    ├── Ark.Tools.editorconfig
    ├── Ark.Tools.*.globalconfig
    └── BannedSymbols.txt
```

Analyzer and tool packages would have to be unconditional nuspec dependencies,
separate optional packages, or explicit consumer references.

**Advantages**

- Familiar `PackageReference` adoption.
- Version can be managed through CPM.
- Build assets can flow transitively when that is intentionally allowed.
- Coexists with every project SDK without wrapping or SDK resolver behavior.
- Suitable for configuration and targets that do not affect restore.

**Disadvantages**

- Cannot conditionally add or change restore-affecting items, including
  `PackageReference`.
- Fixed dependencies make test-only, SQL-excluded, and opt-out analyzer
  profiles coarse or require a package family.
- Imports are generated from a prior restore, which complicates first-restore
  bootstrapping and changes to restore inputs.
- Transitive flow can unexpectedly impose organization policy on downstream
  package consumers unless assets are carefully made private.
- Cannot replace all current `Directory.Build.props` responsibilities.
- Mirrors the now-deprecated `Meziantou.DotNet.CodingStandard` model.

### Comparison

| Dimension | MSBuild SDK | NuGet `buildTransitive` |
| --- | --- | --- |
| Activation | SDK reference in each project | `PackageReference` |
| Central version | `global.json` `msbuild-sdks` | CPM or package version |
| Import points | Implicit top and bottom SDK imports | Generated NuGet props and targets |
| Conditional package references | Supported | Restore-affecting items are excluded |
| Conditional build properties/targets | Supported | Supported if they do not affect restore |
| Configuration/additional files | Supported | Supported |
| Transitive policy propagation | No | Yes, subject to asset metadata |
| Multiple project profiles | Conditions or multiple SDKs | Conditions plus separate packages for dependency differences |
| Existing SDK composition | Additional SDK or wrappers | Native |
| Consumer project simplicity | Small after per-project SDK adoption | One package reference, plus explicit conditional dependencies |
| CPM ownership of injected packages | SDK owns implicit versions | Dependencies or consumer CPM own versions |
| Best fit | Complete solution standard | Analyzer/configuration-only standard |

## Recommendation

Use **Alternative A, one additive `Ark.Tools.Sdk`**, and pin its version once in
`global.json`. Keep the existing Microsoft SDK declaration so the first release
does not need a family of wrapper SDKs. Add another Ark SDK only when a project
type cannot be expressed safely by conditions.

This is the only alternative that can faithfully centralize the current
conditional package references. It also avoids making build policy transitively
leak through Ark.Tools runtime libraries. The traditional package remains a
valid fallback if the accepted scope is reduced to analyzer configuration and
non-restore build behavior.

Defaults should use "set when empty" semantics unless a decision explicitly
marks a policy as mandatory. Every behavior needs a documented opt-out, except
for properties accepted as organization invariants.

## Current Ark.Tools feature inventory

The inventory below was verified against:

- `/Directory.Build.props`;
- `/Directory.Build.targets`;
- `/samples/Ark.ReferenceProject/Directory.Build.props`;
- `/samples/Ark.ReferenceProject/Directory.Build.targets`;
- the root analyzer configuration and banned-symbol files; and
- `/Directory.Packages.props` for package versions.

`Proposed disposition` is a design recommendation, not an accepted decision.

### Early properties

| Current feature/default | Current scope | Proposed disposition |
| --- | --- | --- |
| `TargetFrameworks=net8.0;net10.0` at root; `TargetFramework=net10.0` in ReferenceProject | Repository/sample choice | Exclude. A reusable standard must not silently choose consumer TFMs. |
| Local `Version=999.9.9`; sample `Version=6.6.6` | Local package development | Exclude. Versioning remains repository-owned. |
| `ArkCoreInterceptorsEnabled=true`, compiler-visible property, and `Ark.Tools.Core.Generated` interceptor namespace | Ark.Tools.Core local project-reference support | Exclude initially. Published `Ark.Tools.Core` already carries its consumer build assets. |
| `ContinuousIntegrationBuild=true` for `TF_BUILD`, `GITHUB_ACTIONS`, or `CI`; `_IsGitHubActions=true` | All projects | Include, set only when detected and otherwise preserve an explicit value. |
| `IsPackable=true` at root; `false` in ReferenceProject | Conflicting repository defaults | Do not set globally. Set `false` only for an accepted test profile. |
| `Nullable=enable` | Non-SQL projects | Include when empty. |
| `ImplicitUsings=enable` | Non-SQL projects | Include when empty. |
| `TreatWarningsAsErrors=true` | All current projects | Include only after deciding whether it is unconditional or CI/Release-only. |
| `MSBuildTreatWarningsAsErrors=true` | All current projects | Same policy as compiler warnings. |
| `NoWarn=NU1701;1591;CS1998;NU1605` | All current projects | Do not copy as one blanket list. Decide every suppression; preserve consumer `NoWarn` values when appending. |
| `AllowUnsafeBlocks=true` | All current projects | Make opt-in or confirm as an organization default; it broadens the language surface. |
| `GenerateDocumentationFile=true` | All current projects | Include when empty. |
| `GenerateAssemblyConfigurationAttribute=false` | All current projects | Include only if the generated attribute conflicts with established Ark metadata. |
| `GenerateAssemblyCompanyAttribute=false` | All current projects | Keep package/repository-owned unless all consumers use external assembly metadata. |
| `GenerateAssemblyProductAttribute=false` | All current projects | Keep package/repository-owned unless all consumers use external assembly metadata. |
| `EmbedUntrackedSources=true` | All current projects | Include when empty. |
| `DebugType=portable` | All current projects | Include when empty; do not adopt Meziantou's `embedded` default without a compatibility decision. |
| `DebugSymbols=true` | All current projects | Include when empty, or rely on the Microsoft SDK default if verified redundant. |
| `RestorePackagesWithLockFile=true` | All current projects | Include when empty. |
| `RestoreLockedMode=true` on CI | All current projects | Include when empty and `ContinuousIntegrationBuild=true`. |
| `EnablePackageValidation=true` | All current projects | Restrict to packable projects unless validation proves no cost or failures for applications. |
| `RestoreUseStaticGraphEvaluation=false` | Workaround for NuGet audit suppression issue | Do not fossilize. Re-test NuGet/Home#14300 with supported SDKs before inclusion. |
| `RestoreSerializeGlobalProperties=true` | All current projects | Include when empty. |
| `Deterministic=true` | All current projects | Include when empty. |
| `AccelerateBuildsInVisualStudio=true` | All current projects | Include when empty. |
| `Features=strict` | All current projects | Include when empty after compatibility testing on .NET 8 and .NET 10. |
| `ReportAnalyzer=true` | Non-SQL projects | Include when empty. |
| `EnableNETAnalyzers=true` | Non-SQL projects | Include when empty. |
| `AnalysisLevel=latest-all` | Non-SQL projects | Include when empty; SDK upgrades can intentionally expose new diagnostics. |
| `LangVersion=latest` | Non-SQL projects | Include only if consumers accept compiler behavior changing with SDK upgrades. |
| `EnforceCodeStyleInBuild=true` | Non-SQL projects | Include when empty, with policy strength decided together with warnings-as-errors. |
| `GenerateSBOM=true` | All current projects | Include for pack/publish outputs if `Microsoft.Sbom.Targets` remains an implicit dependency. |
| `PolyUseEmbeddedAttribute=true` | All current projects | Include only with the Polyfill package profile. |
| `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=low` | All current projects | Include when empty. |
| `WarningsNotAsErrors += NU1901;NU1905` | All current projects | Resolve the policy conflict with warnings-as-errors; do not silently preserve low-risk exceptions without review. |
| `IsTestProject=true` for names ending `.Tests` or `.UnitTests` | Convention-based detection | Retain only as fallback; explicit test SDK/profile is less surprising. |
| Test `IsPackable=false` and `WarnOnPackingNonPackableProject=false` | Test projects | Include in the test profile. |
| Test `OutputType=Exe` and `EnableMSTestRunner=true` | Test projects | Keep `OutputType=Exe` for MTP; decide framework-specific runner ownership separately. |
| Test `ExcludeByAttribute=Obsolete,GeneratedCodeAttribute` | Test projects | Include only if this is an accepted cross-framework MTP filter. |
| `ReqnrollUseIntermediateOutputPathForCodeBehind=true` | Test projects | Include in an optional Reqnroll profile, not every test project. |
| `ReqnrollDeleteObsoleteCodeBehindFilesOnClean=true` | Test projects | Include with the Reqnroll profile. |
| `TreatTSqlWarningsAsErrors=True`, `RunSqlCodeAnalysis=True` | ReferenceProject SQL projects | Add only through an explicit SQL-compatible profile; general C# analyzers remain excluded. |
| `EnableSourceControlManagerQueries=false`, `EnableSourceLink=false` when `COPILOT_AGENT_ACTION` is set | Copilot sandbox workaround | Include while the sandbox limitation remains reproducible. |
| `ApplicationInsightsResourceId=/subscriptions/dummy` | Historical local telemetry workaround | Exclude unless a current failing scenario proves it is still required. |

### Analyzer and configuration assets

| Current asset | Current behavior | Proposed disposition |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.400 | Private analyzer reference for non-SQL projects | Include implicitly unless the platform analyzer is sufficient; version owned by the SDK. |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` 4.14.0 | Private analyzer reference for non-SQL projects | Include implicitly. |
| `Meziantou.Analyzer` 3.0.160 | Private analyzer reference for non-SQL projects | Include implicitly. |
| `Microsoft.VisualStudio.Threading.Analyzers` 18.7.23 | Private analyzer reference for non-SQL projects | Include implicitly. |
| `ErrorProne.NET.CoreAnalyzers` 0.1.2 | Private reference; root supports `DisableErrorProneAnalyzers=true` | Include only if accepted, with the existing opt-out. |
| `.netanalyzers.globalconfig` | 97 CA/IDE severity overrides | Package and load as a global analyzer config. |
| `.meziantou.globalconfig` | 34 MA severity overrides | Package and load as a global analyzer config. |
| `.errorprone.globalconfig` | 30 EPC/ERP severity overrides | Package with ErrorProne and load only when enabled. |
| `.vsthreading.globalconfig` | 23 VSTHRD severity overrides | Package and load as a global analyzer config. |
| `.editorconfig` | Formatting, code-style, naming rules, and three error severities | Split build-enforced analyzer settings from source-tree editor formatting. Verify Visual Studio and Rider design-time behavior before removing the checked-in file. |
| Consumer `.globalconfig` | ReferenceProject keeps a local override file | Preserve local override capability and document precedence. |
| `BannedSymbols.txt` | 93 active bans: local time, ambiguous parsing/rounding/culture, reference tuples, implicit time-zone conversion, console logging, and blocking task/thread APIs | Package as `AdditionalFiles`; provide one opt-out property and support a consumer-owned additional banned-symbol file. |
| `Disable_SponsorLink` target | Removes `DevLooped.SponsorLink` and `Moq.CodeAnalysis` analyzers | Include with an opt-out, matching current behavior. |
| Root wildcard imports for `.*.globalconfig` and `.*.editorconfig` | Loads repository-local analyzer overrides for non-SQL projects | Keep local discovery in addition to packaged configs; avoid duplicate imports. |

Analyzer versions shown are the versions pinned on 2026-08-29. The SDK package
must test analyzer upgrades as product changes rather than inherit arbitrary
consumer CPM versions.

### Late items and targets

| Current feature | Current behavior | Proposed disposition |
| --- | --- | --- |
| `Polyfill` private package reference | Added to all root projects | Include only if its generated embedded attributes are an accepted baseline; otherwise let libraries opt in. |
| `Microsoft.SourceLink.GitHub` private package reference | Added to packable root projects | Include conditionally for packable GitHub-hosted projects. |
| `Microsoft.Sbom.Targets` private package reference | Added to non-SQL projects | Include conditionally with `GenerateSBOM`; confirm whether applications and test projects need it. |
| Global usings | Adds `System.Diagnostics.CodeAnalysis`, `System.Globalization`, and `System.Text` for C# with implicit usings | Include with an opt-out or decide that standards should not alter source name resolution. |
| `appsettings*.json` | Base files always copied to output/publish; environment variants copied to output but never publish | Do not apply to every project. Restrict to an explicit application/web profile after publish semantics are confirmed. |
| `reqnroll*.json` | Always copied to test output | Include only in the Reqnroll profile. |
| `testconfig.json` | Copied with `PreserveNewest` | Include only in the test profile. |
| MTP extension package references | Crash dump, code coverage, hang dump, hot reload, retry, TRX, and Azure DevOps report | Include in the test profile, with individual feature switches where extensions have runtime or licensing implications. |
| Test framework packages | `MSTest.TestAdapter`, `MSTest.TestFramework`, `MSTest.Analyzers`, `Microsoft.NET.Test.Sdk`, and `AwesomeAssertions` | Current behavior, but do not place in the base SDK. Resolve whether the test profile owns MSTest/Reqnroll or remains framework-neutral. |
| Exact project-reference version target | Rewrites packed project dependencies as exact versions | Include only in a pack profile after package-validation tests. |
| Ark icon and package metadata | Ark.Tools repository URL, project URL, MIT license, authors, copyright, symbols/snupkg | Never copy Ark.Tools repository URLs into consumers. A separate organization pack profile may supply safe author/license defaults. |
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
| Command line | Adds TRX, mini crash dumps, ten-minute mini hang dumps, CI coverage, and a minimum expected test count of one. | Strong candidates; values need acceptance and CI validation. |
| Empty-run protection | Defaults `MinimumExpectedTests=1`, with `0` disabling the explicit argument. | Include to prevent false-green test runs. |
| Test optimization | Disables analyzers before `_MTPBuild` unless opted out. | Adopt only if CI has a separate analyzed build, as the upstream comment assumes. |
| CI reporting | Enables GitHub annotations/report with slow-test notices disabled. | Select GitHub or Azure DevOps extension from detected CI. |
| Runner selection | Requires MTP selection in `global.json`; does not add `Microsoft.NET.Test.Sdk` or VSTest settings. | Ark currently still adds `Microsoft.NET.Test.Sdk`; migration needs an explicit decision and proof. |

### Lessons from `Meziantou.DotNet.CodingStandard`

The predecessor packages style/analyzer configuration and imports its
`build` props/targets again through `buildTransitive`. Its analyzer packages are
unconditional nuspec dependencies. It supports reproducible builds, analyzer
defaults, NuGet audit, global usings, packaging metadata, banned symbols,
SponsorLink removal, and VSTest analyzer suppression.

Its repository now states that it is deprecated and replaced by
`Meziantou.NET.Sdk`. The migration validates the architectural conclusion:
`buildTransitive` is adequate for static policy, while an SDK is better when
project-type-aware dependencies and broader build orchestration are required.

## Configuration layering

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
- additional-SDK composition with `Microsoft.NET.Sdk`,
  `Microsoft.NET.Sdk.Web`, and `MSBuild.Sdk.SqlProj`;
- .NET 8 and .NET 10 single- and multi-target projects;
- CPM with no duplicate `PackageVersion` for implicit packages;
- package lock-file generation and locked CI restore;
- local override precedence for every packaged analyzer config type;
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
- [`.editorconfig`](../../.editorconfig)
- [`.netanalyzers.globalconfig`](../../.netanalyzers.globalconfig)
- [`.meziantou.globalconfig`](../../.meziantou.globalconfig)
- [`.errorprone.globalconfig`](../../.errorprone.globalconfig)
- [`.vsthreading.globalconfig`](../../.vsthreading.globalconfig)
- [`BannedSymbols.txt`](../../BannedSymbols.txt)

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
