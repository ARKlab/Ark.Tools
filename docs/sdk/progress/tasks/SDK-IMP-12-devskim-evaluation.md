# SDK-IMP-12 — Microsoft.CST.DevSkim evaluation

**Status**: Analysis complete · **Category**: Analyzer evaluation
**Depends on**: SDK-IMP-04

## Problem

Evaluate whether `Microsoft.CST.DevSkim` should be added to the Ark.Tools SDK
analyzer baseline.

## Analysis scope

- Confirm supported target frameworks, project types, and analyzer delivery
  model.
- Inventory the diagnostics and configuration needed for repository consumers.
- Compare coverage with the existing analyzer baseline and identify any
  duplicate or conflicting diagnostics.
- Define configuration, severity, opt-out, SQL exclusion, version ownership,
  and lock-file requirements.
- Validate the impact on existing repository projects and clean-consumer
  fixtures before implementation.

## Compatibility and delivery findings

The current `Microsoft.CST.DevSkim` release is **1.0.90**. Its NuGet
metadata targets `netstandard2.1`, `net8.0`, `net9.0`, and `net10.0`, and
describes the package as a library. The package contains assemblies under
`lib/` only; it does not contain Roslyn assemblies under `analyzers/`, nor
MSBuild integration under `build/` or `buildTransitive/`.

The library embeds the DevSkim rules and exposes a text/file-name processing
API (`DevSkimRuleSet` and `DevSkimRuleProcessor`). It is not a Roslyn
`DiagnosticAnalyzer`, so adding it as an SDK `PackageReference` would restore
the library and its `Microsoft.CST.ApplicationInspector.RulesEngine` and
`Newtonsoft.Json` dependencies without producing compiler diagnostics. The
DevSkim CLI and IDE extensions are the supported analysis entry points for
repository files.

Sources:

- [NuGet package metadata for 1.0.90](https://api.nuget.org/v3-flatcontainer/microsoft.cst.devskim/1.0.90/microsoft.cst.devskim.nuspec)
- [DevSkim library project](https://github.com/microsoft/DevSkim/blob/main/DevSkim-DotNet/Microsoft.DevSkim/Microsoft.DevSkim.csproj)
- [DevSkim rule processor](https://github.com/microsoft/DevSkim/blob/main/DevSkim-DotNet/Microsoft.DevSkim/DevSkimRuleProcessor.cs)
- [DevSkim CLI usage](https://github.com/microsoft/DevSkim#basic-usage)

## Rule inventory

The default rule set in the 1.0.90 source contains 48 JSON files, 127 rule
entries, and 123 unique rule IDs. Rule severities are DevSkim values
(`critical`, `important`, `moderate`, `ManualReview`, and
`BestPractice`), not Roslyn diagnostic severities. The rule set covers
security APIs, cryptography, TLS, control flow, frameworks, hygiene,
privacy, storage, XML, vulnerable libraries, and one correctness group.

The C#-specific entries are:

```text
DS184626 DS109501 DS106864 DS156431 DS126187 DS440020 DS440071
DS148264 DS144436 DS168931 DS112835 DS112836 DS112837 DS112839
DS440075 DS425040 DS113854 DS172412 DS112266
```

Additional language-agnostic rules can match C# based on their file patterns.
The default set also contains `DS224000` for dangerous T-SQL commands, but
that rule is useful only when the DevSkim scanner is explicitly run over SQL
source; it is not SQL-project MSBuild integration.

## Coverage and duplicate resolution

| Concern | DevSkim coverage | Existing Ark.Tools coverage | Resolution |
| --- | --- | --- | --- |
| Weak/broken hashes and ciphers | `DS126858`, `DS168931`, `DS109501`, `DS106864`, `DS156431` | `CA5350`, `CA5351` | Keep the .NET analyzer authoritative. |
| Certificate validation | `DS126187` | `CA5359` | Keep the .NET analyzer authoritative. |
| Deserialization | `DS425040` | `CA5360` | Keep the .NET analyzer authoritative. |
| Deprecated or hard-coded TLS | `DS144436`, `DS440020`, `DS112835`–`DS112839`, `DS440075` | `CA5364`, `CA5397` | Keep the .NET analyzer authoritative. |
| Async, threading, logging, culture, and API bans | No equivalent DevSkim diagnostic namespace | `VSTHRD*`, `MA*`, `EPC*`, `ERP*`, `IDX00001`, `ARKCORE005`, and Ark banned symbols | No change. |
| T-SQL dangerous commands | `DS224000` | No equivalent C# diagnostic | Keep outside the SDK; use a dedicated SQL scanning step if needed. |

The overlap is semantic rather than diagnostic-ID duplication: DevSkim emits
`DS*` findings through its own rule engine, while the existing baseline emits
Roslyn diagnostics. Layering both would produce duplicate security findings
for the rows above. Because DevSkim cannot run as an SDK Roslyn analyzer, the
existing .NET analyzer rules remain authoritative and are not disabled. No
DevSkim configuration is added to suppress or remap those rules.

## Configuration, SQL, version, and lock-file decisions

- **Configuration and severity:** Do not add a global analyzer config or
  `dotnet_diagnostic` entries. DevSkim severity, confidence filtering, rule
  selection, custom rules, and inline suppressions belong to its CLI/library
  rule-processing model.
- **Opt-out:** Do not add `EnableArkToolsDevSkim`; there is no SDK-injected
  analyzer or configuration asset to switch off.
- **SQL:** Do not alter the SDK SQL exclusion. The SDK continues to exclude
  C# analyzers from `UsingMicrosoftBuildSqlSdk` projects. `DS224000` remains
  available only to an explicitly invoked DevSkim scan, not to SQL project
  builds.
- **Version ownership:** Do not give the SDK ownership of
  `Microsoft.CST.DevSkim`. If repository scanning is later desired, pin
  `Microsoft.CST.DevSkim.CLI` as a separate tool or CI integration. A future
  Roslyn adapter would need to follow SDK-IMP-04 exact, private,
  SDK-owned analyzer references and non-SQL conditions.
- **Lock files:** This evaluation adds no package or tool restore input, so no
  `packages.lock.json` file changes are required. Any future SDK package
  adoption would require exact-version lock-file updates for every affected
  consumer fixture and repository project.

## Recommendation and validation

**Reject adding `Microsoft.CST.DevSkim` to the Ark.Tools SDK analyzer
baseline.** The package is a general text-analysis library, not a build-time
Roslyn analyzer; adding it would increase the restore graph without improving
SDK compiler analysis and would duplicate existing security coverage if a
custom adapter were layered on top.

No SDK implementation follow-up is approved. A separate future task may
evaluate the DevSkim CLI as an opt-in CI security scan, with SARIF output and
explicit source-language/SQL scope; that is outside the SDK analyzer baseline.
The package metadata, source project, embedded rule inventory, existing
analyzer configuration, and clean-consumer SDK topology were reviewed. Since
the proposal is rejected and no restore/build assets changed, no repository
fixture or project changes are necessary.

## Acceptance

- [x] The compatibility and coverage analysis is documented.
- [x] Duplicate or conflicting diagnostics have an explicit resolution.
- [x] A follow-up implementation scope is approved or the proposal is rejected.
