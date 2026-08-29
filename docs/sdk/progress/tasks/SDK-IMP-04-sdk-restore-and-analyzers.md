# SDK-IMP-04 — SDK restore policy and analyzer ownership

**Category**: sdk-policy · **Priority**: foundation
**Depends on**: SDK-IMP-01, SDK-IMP-03
**Scope**: SDK PROPS + RESTORE + ANALYZERS + TESTS
**Design**: [SDK-only boundary](../../design.md#kept-in-arktoolssdk-not-public-transitively),
[Accepted decisions](../../design.md#accepted-decisions)

## Problem

Restore-affecting policy cannot come from `buildTransitive`. The additional SDK
must set restore/compiler policy early, own exact analyzer versions, and keep
package and configuration opt-outs synchronized without depending on consumer
central package management.

## Execution map

- **CI detection**: preserve an explicit `ContinuousIntegrationBuild`; otherwise
  set it for `TF_BUILD`, `GITHUB_ACTIONS`, or `CI`. Preserve the accepted
  `_IsGitHubActions` signal needed by later SDK behavior.
- **Restore**: set when empty `RestorePackagesWithLockFile=true`,
  `RestoreSerializeGlobalProperties=true`, and
  `RestoreLockedMode=true` only for a continuous-integration build.
- **Audit**: set when empty `NuGetAudit=true`, `NuGetAuditMode=all`, and
  `NuGetAuditLevel=low`. Add no audit warning exceptions.
- **Compiler version policy**: for non-SQL C# projects set when empty
  `AnalysisLevel=latest-all` and `LangVersion=14.0`.
- **Core classification**: preserve explicit `IsTestProject`; only when empty,
  set it for `.Tests` and `.UnitTests` project-name suffixes. Later test/content
  profiles consume this shared result.
- **Analyzer injection**: add exact, implicit, private references to
  `Microsoft.CodeAnalysis.NetAnalyzers`,
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`, `Meziantou.Analyzer`,
  `Microsoft.VisualStudio.Threading.Analyzers`, and
  `ErrorProne.NET.CoreAnalyzers`.
- **Opt-outs**: the same `EnableArkTools*` switch removes an analyzer reference
  in the SDK and its configuration item in Build. A disabled feature leaves
  neither package nor config.
- **SQL**: exclude every C# analyzer reference and compiler-version policy when
  `UsingMicrosoftBuildSqlSdk == 'true'`.
- **CPM**: exact versions are SDK-owned. A consumer must remove matching
  `PackageVersion` entries; no version-override property is introduced.
- **Negative boundary**: inject no test framework, assertion, Reqnroll,
  `Microsoft.NET.Test.Sdk`, SourceLink, SBOM, Polyfill, or MTP extension in this
  task.

## Implementation steps

1. Place restore-affecting properties in the earliest SDK import that can affect
   the current restore and verify import order.
2. Add exact package references with `IsImplicitlyDefined=true`, appropriate
   private assets, and the accepted asset filters.
3. Connect analyzer opt-outs to the Build switches without allowing late Build
   targets to pretend an already-restored package was removed.
4. Extend clean consumers for local, Azure Pipelines, GitHub Actions, generic
   CI, explicit override, test classification, SQL, and CPM cases.
5. Generate and exercise lock files from an empty cache; change a lock input and
   prove locked CI restore fails.
6. Add a negative CPM fixture with a duplicate SDK-owned `PackageVersion` and
   assert the actionable `NU1009` boundary.

## Required test coverage

- CI detection selects the intended signals and never overwrites an explicit
  value.
- Every project generates a lock file; only CI defaults to locked mode.
- Audit mode and level evaluate exactly as selected and remain overrideable.
- C# 14 and latest-all apply only through explicit SDK activation and can be
  overridden by a project.
- Explicit test classification wins; suffix fallback applies only when unset.
- Every analyzer resolves at the exact SDK-owned version and is private.
- Each analyzer opt-out removes both package and configuration.
- SQL fixtures receive no C# analyzer or compiler-version policy.
- Compliant CPM restores without duplicate versions; duplicate SDK-owned CPM
  entries fail with `NU1009`.

## Outcomes

- Restore, audit, language, and analyzer versions form one tested SDK release.
- Consumers upgrade tooling by upgrading the SDK and refreshing lock files.
- Build-only transitive consumers receive no restore or package changes.

## Acceptance

- [ ] CI, lock-file, locked-restore, serialized-restore, and audit policy are
  implemented and tested.
- [ ] C# language/analysis defaults are SDK-only and overrideable.
- [ ] Exact analyzer references and every package/config opt-out are tested.
- [ ] SQL and CPM boundaries are covered by positive and negative fixtures.
- [ ] No framework, assertion, Reqnroll, VSTest, or later-task package leaks.
- [ ] The [task board](README.md) status for SDK-IMP-04 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
