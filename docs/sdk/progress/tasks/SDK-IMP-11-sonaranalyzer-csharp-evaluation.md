# SDK-IMP-11 — SonarAnalyzer.CSharp evaluation

**Status**: Complete — baseline adoption rejected; optional consumer use remains
possible
**Category**: Analyzer evaluation
**Depends on**: SDK-IMP-04
**Reviewed**: 2026-09-04

## Problem

Evaluate whether `SonarAnalyzer.CSharp` should be added to the Ark.Tools SDK
analyzer baseline.

## Analysis method and evidence

The evaluation reviewed:

- SonarSource's [`sonar-dotnet` README](https://github.com/SonarSource/sonar-dotnet),
  rule metadata, and the `10.33.0.1635` release
  ([release notes](https://github.com/SonarSource/sonar-dotnet/releases/tag/10.33.0.1635)).
- The `SonarAnalyzer.CSharp 10.33.0.1635` NuGet package and nuspec.
- The analyzer IDs embedded in the package DLL: 483 distinct `S` identifiers.
  This agrees with the upstream description of 480+ C# rules.
- The Ark baseline in
  [`docs/analyzers.md`](../../../analyzers.md), the packaged global configs,
  and the exact SDK-owned references in
  [`Ark.Tools.Sdk`](../../../../src/sdk/Ark.Tools.Sdk/common/common.targets).

The reviewed package contains one C# analyzer DLL, no target-framework-specific
compile/runtime assets, and no runtime dependency graph. It is therefore
technically compatible with the repository's C# projects targeting .NET 8 and
.NET 10, but it is a build-time analyzer only. The package is distributed under
the [Sonar Source-Available License v1.0 (SSALv1)](https://www.sonarsource.com/license/ssal/),
not the MIT license used by Ark.Tools. That licensing difference requires
explicit legal and redistribution approval before any SDK baseline inclusion.

## Rule inventory

The package covers substantially more than a focused compiler-analyzer baseline:

| Area | Representative rules | Benefit |
| --- | --- | --- |
| Bugs and control flow | `S2259` null dereference, `S2583` unreachable branches, `S3923` equivalent branches | Finds correctness issues beyond syntax and type checking. |
| Resource and API correctness | `S2931` disposable members, `S3881` dispose pattern, `S3966` repeated disposal | Adds lifecycle checks for framework and library code. |
| Security | `S2068` hard-coded credentials, `S2077` dynamically formatted SQL, `S2245` weak PRNG use, `S4830` certificate validation | Adds security-focused checks not provided by the current general baseline. |
| Maintainability and complexity | `S107` parameter count, `S1192` duplicated literals, `S3358` nested conditional expressions, `S3776` cognitive complexity | Provides broad code-smell and maintainability reporting. |
| Performance and framework guidance | `S1854` dead stores, `S2325` make members static, `S6602` `Find` versus `FirstOrDefault`, `S9129` EF `Include` chain optimization | Adds many optimization suggestions, often overlapping existing analyzers or application-specific policy. |

The security and SonarQube/SonarCloud quality-profile integration are the main
potential benefits. The standalone package does not provide the Sonar server,
quality profiles, taint-analysis workflow, metrics, coverage import, or PR
reporting. Those benefits require the broader Sonar ecosystem, which is not an
Ark.Tools SDK dependency or requirement.

## Overlap and ownership

The current baseline is deliberately specialized: 97 configured .NET/IDE
diagnostics, 34 Meziantou diagnostics, 30 ErrorProne diagnostics, 23 Visual
Studio Threading diagnostics, and the Ark banned-symbol set. The overlap is
not only by diagnostic ID; several analyzers report the same code smell with
different IDs.

| Sonar rule(s) or area | Existing owner | Decision if Sonar is ever enabled |
| --- | --- | --- |
| `S101`, `S100`, naming and style rules | .NET/IDE configuration and the packaged coding-style EditorConfig | Existing naming/style policy remains authoritative; disable Sonar naming/style duplicates. |
| `S1854` dead local assignments | Compiler/IDE `IDE0059` | Disable `S1854`; keep the compiler diagnostic. |
| `S4487` unread private fields | .NET `CA1823` (error) | Disable `S4487`; keep `CA1823`. |
| `S2325` member can be static | .NET `CA1822` | Disable `S2325`; keep `CA1822`. |
| `S2931` and `S3881` disposable implementation checks | .NET `CA1063` and related `CA2213`/`CA2215` rules | Disable the overlapping Sonar disposal rules; keep the existing .NET ownership. |
| `S4462` blocking calls to async methods | Meziantou `MA0042`/`MA0045`, ErrorProne `EPC35`, and the specialized VS Threading rules | Disable `S4462`; keep the existing async/threading ownership. |
| Certificate and weak-cryptography checks, including `S4830` and `S4426` | .NET `CA5350`, `CA5351`, `CA5359`, `CA5364`, `CA5385`, and `CA5397` | Keep the existing .NET security rules for equivalent API patterns. Enable a Sonar rule only after a version-specific comparison proves it is complementary. |
| `S112`, `S2068`, `S2077`, `S2245`, `S3776`, and framework-specific rules | No exact current owner | These are the strongest incremental coverage, but they do not justify enabling the entire 480+ rule set. |

The complete Sonar catalog must not be treated as a one-to-one replacement for
the existing analyzers. Rule IDs and behavior change between Sonar releases;
an ownership map must be regenerated for every candidate version. In
particular, `TreatWarningsAsErrors=true` would turn Sonar's default warnings
into build failures, and the current test profile intentionally optimizes test
runs by disabling analyzers unless explicitly restored.

## Configuration and delivery decision

Do **not** add `SonarAnalyzer.CSharp` to the SDK baseline. Consequently:

- No `Ark.Tools.SonarAnalyzer.globalconfig` is added.
- No default severity or Sonar rule allowlist is imposed.
- No `EnableArkToolsSonarAnalyzer` switch is added.
- No Sonar package reference, transitive dependency, or lock-file entry is
  added.
- SQL projects remain excluded from C# analyzer injection through the existing
  `UsingMicrosoftBuildSqlSdk == 'true'` boundary.
- Existing exact SDK-owned analyzer versions and their independent opt-outs
  remain unchanged.

If a consumer requires Sonar integration, it should reference the package
directly and own its SonarQube/SonarCloud profile. The reference should be
private to the consuming project and should not be copied into Ark.Tools
library packages. A future Ark opt-in must first provide an exact-version
global config, an independent switch, explicit duplicate suppressions, SQL
and test fixtures, clean-consumer validation, and refreshed lock files. It
must also pass legal review for SSALv1.

## Benefits evaluation

| Benefit | Assessment |
| --- | --- |
| Security coverage | Meaningful incremental coverage, especially credentials, SQL formatting, PRNG, certificate, and cryptographic-key rules. |
| Bug and reliability detection | Useful, but partially duplicated by nullable analysis, .NET analyzers, Meziantou, ErrorProne, and VS Threading. |
| Maintainability metrics | Valuable inside SonarQube quality gates, but not a strong reason to impose standalone build diagnostics. |
| Developer experience | Poor fit for a default SDK baseline: 483 diagnostics create noise, review burden, and warning-as-error compatibility risk. |
| Operational cost | High: rule churn, version-specific overlap review, longer analysis, lock-file updates, and suppression maintenance. |
| Distribution risk | SSALv1 requires legal approval and is inconsistent with an unconditional MIT-oriented SDK baseline. |

**Recommendation:** reject baseline adoption. The security benefit is real but
does not outweigh broad overlap, warning noise, operational cost, and licensing
risk for a framework-neutral SDK. Organizations already using SonarQube or
SonarCloud can opt in at the application level with their own quality profile.

## Follow-up scope

No implementation task is approved by this evaluation. Re-open SDK-IMP-11 only
when an Ark consumer supplies a concrete SonarQube/SonarCloud requirement and
legal approval. The reopened task must:

1. Pin and scan one exact package version.
2. Generate a complete rule inventory and version-specific overlap matrix.
3. Define the Sonar severity profile and disable every duplicate owned by the
   current baseline.
4. Add an independent opt-in and private package reference without changing
   default consumer behavior.
5. Prove non-SQL, SQL, test, clean-consumer, package, and locked-restore
   fixtures.

## Acceptance

- [x] The overlap analysis, benefits evaluation, and recommendation are
  documented.
- [x] Duplicate diagnostics have explicit ownership and disable decisions.
- [x] Configuration, severity, opt-out, SQL exclusion, version ownership, and
  lock-file requirements are documented.
- [x] The baseline-adoption proposal is rejected and the conditions for a
  future opt-in are defined.
