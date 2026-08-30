# SDK-IMP-02 — Public `Ark.Tools.Build` safety baseline

**Category**: build-policy · **Priority**: foundation
**Depends on**: SDK-IMP-01
**Scope**: BUILD PROPS + SQL PROPS + TESTS
**Design**: [Selected public transitive baseline](../../design.md#selected-public-transitive-baseline)

## Problem

`Ark.Tools.Build` is deliberately public and can reach an unknown package
consumer. It must provide the accepted compiler-safety baseline without
selecting packages, inferring project roles, changing artifacts, or overriding
consumer intent.

## Execution map

- **All projects**: set `TreatWarningsAsErrors=true` and
  `MSBuildTreatWarningsAsErrors=true` only when empty.
- **Non-SQL C#**: when `MSBuildProjectExtension == '.csproj'` and
  `UsingMicrosoftBuildSqlSdk != 'true'`, set when empty:
  `Nullable=enable`, `ImplicitUsings=enable`,
  `GenerateDocumentationFile=true`, `Features=strict`,
  `ReportAnalyzer=true`, and `EnforceCodeStyleInBuild=true`.
- **SQL**: only when `UsingMicrosoftBuildSqlSdk == 'true'`, set
  `TreatTSqlWarningsAsErrors=true` and `RunSqlCodeAnalysis=true` when empty.
- **Overrides**: `EnableArkToolsBuild=false` disables the complete baseline when
  set before package props import. A project body can override each property
  after import, including `GenerateDocumentationFile=false` and
  `ImplicitUsings=disable`.
- **Native defaults**: do not set `DebugType`, `DebugSymbols`, `Deterministic`,
  `EmbedUntrackedSources`, or `EnableNETAnalyzers`.
- **Negative boundary**: add no `PackageReference`, framework selection,
  global using, content metadata, test topology, publish behavior, pack
  behavior, `NoWarn`, unsafe setting, identity, or organization metadata.

## Implementation steps

1. Add the property groups to the canonical Build props with exact
   set-when-empty and project-capability conditions.
2. Keep the whole-package switch outside the guarded implementation so disabled
   consumers import no baseline.
3. Extend the clean-consumer fixture with C#, overridden C#, disabled C#, and
   `Microsoft.Build.Sql` projects.
4. Snapshot evaluated properties and relevant items; fail on any additional
   public property or item not listed by this task.
5. Add compatibility assertions for native defaults rather than copying those
   values into Build.

## Required test coverage

- A plain C# consumer receives every and only accepted C# property.
- A project-body value wins for every property; explicit `false`/`disable`
  values are preserved.
- `EnableArkToolsBuild=false` set in `Directory.Build.props` and as a global
  property suppresses the baseline.
- A SQL project receives only all-project and SQL properties, never C# policy.
- An unrelated non-C# project receives only the two all-project warning
  properties.
- Evaluated items prove there are no injected packages, global usings, content,
  test, publish, or pack changes.
- Native-default fixtures detect an upstream .NET SDK default change without
  forcing the old value.

## Outcomes

- Public transitive consumers receive the narrow accepted safety baseline.
- Every default is project-overridable, and the entire package has an early
  escape hatch.
- SQL behavior is selected by an explicit SDK capability, not project-name
  inference.

## Acceptance

- [x] Every selected Build property is implemented with its exact condition.
- [x] Project-level overrides and the whole-package escape hatch are tested.
- [x] Negative-boundary snapshots contain no SDK-only behavior.
- [x] SQL and non-C# fixtures prove capability-safe selection.
- [x] Native .NET SDK defaults remain platform-owned.
- [x] The [task board](README.md) status for SDK-IMP-02 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
