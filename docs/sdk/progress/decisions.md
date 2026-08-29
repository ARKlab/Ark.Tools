# Ark.Tools SDK — open decisions

Status: **review requested**.

## How to answer

Reply to each decision in PR comments with the option letter, amendments, or
additional constraints. Example: `SDK-01: A`, `SDK-05: B, but only for projects
ending in .IntegrationTests`.

Do not begin implementation until SDK-01 through SDK-08 are decided. Later
decisions may be implemented as isolated profiles after the baseline exists.

## SDK-01 — Distribution model

**Question:** Is the product a full solution build standard or only a portable
analyzer/configuration package?

### Solution alternatives

- **A — Additive MSBuild SDK.** Supports conditional implicit package
  references, early defaults, and project profiles; every project opts in.
- **B — NuGet `buildTransitive` package.** Familiar and CPM-managed, but cannot
  add restore-affecting items and may flow policy to downstream consumers.
- **C — Both from the first release.** Two activation paths and import-order
  matrices with overlapping behavior.

### Recommendation

**A.** Current requirements include conditional analyzer and test dependencies,
which NuGet excludes from package build assets. Reject C until a real consumer
cannot use an SDK.

**Requested answer:** A, B, or C. If B, identify which current conditional
package features can be removed.

## SDK-02 — SDK activation shape

**Question:** Should Ark replace/wrap the project's primary SDK or compose as an
additional SDK?

### Solution alternatives

- **A — One additional `Ark.Tools.Sdk`.** Projects keep
  `Microsoft.NET.Sdk`, `.Web`, SQL, or another primary SDK.
- **B — Wrapper family.** Publish base, Web, Test, Razor, and other Ark SDKs,
  following Meziantou.
- **C — Additional base SDK plus a dedicated Test SDK.**

### Recommendation

**A for the first release.** It minimizes packages and preserves third-party
SDK composition. Add a specialized SDK only when tests prove conditions cannot
express a profile safely.

**Requested answer:** A, B, or C, plus every primary SDK that must be supported
at launch.

## SDK-03 — Baseline consumers

**Question:** Who is the standard for?

### Solution alternatives

- **A — Any public Ark.Tools NuGet consumer.** Defaults must be conservative
  and organization-neutral.
- **B — ARK-owned line-of-business repositories.** Organization conventions,
  authorship, and stronger policies may be defaults.
- **C — Both, with explicit `ArkSdkProfile=Public|Ark`.**

### Recommendation

**C** if one artifact is required; otherwise publish the public baseline first
and layer an ARK profile later. Never emit the Ark.Tools repository URL into an
unrelated package.

**Requested answer:** A, B, or C. List any defaults that are mandatory only for
ARK-owned repositories.

## SDK-04 — Default versus enforced policy

**Question:** May consumers override SDK values?

### Solution alternatives

- **A — Defaults when empty.** Repository/project values win.
- **B — Mandatory policy.** SDK values overwrite project choices.
- **C — Defaults locally, mandatory in CI.**

### Recommendation

**A**, with explicit validation targets only for a short list of approved
invariants. Silent overwrites make diagnosis and adoption harder.

**Requested answer:** A, B, or C, and the exact non-overridable invariants.

## SDK-05 — Test profile and framework ownership

**Question:** Should the SDK remain MTP-only, or continue adding the current
MSTest and assertion packages?

The current targets add MSTest, `Microsoft.NET.Test.Sdk`, and
`AwesomeAssertions`. The research request excludes framework-specific
Meziantou xUnit/MSTest features but explicitly retains MTP features. This does
not establish whether Ark's own MSTest defaults should remain.

### Solution alternatives

- **A — Framework-neutral MTP profile.** Add MTP extensions/settings only;
  projects select MSTest, NUnit, TUnit, or another MTP framework.
- **B — Ark MSTest profile.** Add current MSTest and AwesomeAssertions packages,
  with a separate neutral profile available.
- **C — No test packages.** Only configure projects that already reference MTP
  components.

### Recommendation

**B** for compatibility with ReferenceProject, implemented as an explicit
profile; keep the base SDK framework-neutral. Remove `Microsoft.NET.Test.Sdk`
only after an MTP migration test proves it is redundant.

**Requested answer:** A, B, or C. Confirm whether Reqnroll/MSTest remains the
standard BDD stack.

## SDK-06 — Test project selection

**Question:** How is the test profile activated?

### Solution alternatives

- **A — Explicit property/profile.**
- **B — Project-name suffix `.Tests` or `.UnitTests`.**
- **C — Explicit activation first, suffix detection as fallback with a build
  message.**

### Recommendation

**C** during migration, then A in a later major version. Suffix-only detection
misses integration/architecture test names and can misclassify projects.

**Requested answer:** A, B, or C, and all recognized test-project suffixes.

## SDK-07 — Analyzer package ownership and CPM

**Question:** Does the SDK pin analyzer/tool versions, or may consumer CPM
control them?

### Solution alternatives

- **A — SDK-owned implicit versions.** Mark references
  `IsImplicitlyDefined=true`; consumers must remove matching `PackageVersion`
  entries.
- **B — Consumer-owned versions.** SDK adds no package references and fails with
  guidance when required analyzers are absent.
- **C — SDK defaults with an advanced version-override property per package.**

### Recommendation

**A.** Configuration and analyzer versions form one tested product. C recreates
an unsupported version matrix; use an SDK upgrade instead.

**Requested answer:** A, B, or C. Confirm whether lock files should record all
implicit packages.

## SDK-08 — Analyzer configuration and source-tree `.editorconfig`

**Question:** Is build enforcement sufficient, or must adoption also configure
format-on-type and non-MSBuild editors?

### Solution alternatives

- **A — Package all configuration only.** No files are copied into repositories.
- **B — Package analyzer/global config, retain a small checked-in
  `.editorconfig` for editor formatting.**
- **C — Provide a separately invoked template/update command that materializes
  `.editorconfig`; the build SDK never writes source files.**

### Recommendation

**B** initially. Package loading can enforce analyzer rules, but source-tree
EditorConfig discovery and non-MSBuild tools must be verified before deleting
the physical file. Never mutate a repository during build.

**Requested answer:** A, B, or C. Name required IDEs: Visual Studio, Rider,
VS Code, or others.

## SDK-09 — Warning strictness

**Question:** When are compiler, MSBuild, code-style, and NuGet audit warnings
errors?

### Solution alternatives

- **A — Always**, matching current Ark files.
- **B — CI and Release**, matching Meziantou's normal policy.
- **C — CI only.**

### Recommendation

**B**, while local Debug still reports warnings. Independently decide whether
NuGet advisories at `low` severity should fail; current
`WarningsNotAsErrors=NU1901;NU1905` conflicts with a fully strict policy.

**Requested answer:** A, B, or C, plus the desired failure threshold for NuGet
audit and the fate of `NU1901`/`NU1905`.

## SDK-10 — Existing warning suppressions

**Question:** Which blanket suppressions remain?

### Solution alternatives

- **A — Preserve `NU1701;CS1591;CS1998;NU1605`.**
- **B — Remove all and fix or locally suppress each occurrence.**
- **C — Keep only reviewed compatibility suppressions with a rationale in the
  SDK.**

### Recommendation

**C.** `NU1605` can hide dependency downgrades, `NU1701` can hide incompatible
assets, and `CS1998`/`CS1591` are policy choices. Each requires an explicit
answer.

**Requested answer:** For each of `NU1701`, `CS1591`, `CS1998`, and `NU1605`,
state keep/remove and why.

## SDK-11 — Unsafe code and latest language features

**Question:** Should `AllowUnsafeBlocks=true`, `LangVersion=latest`, and
`Features=strict` apply to every consumer?

### Solution alternatives

- **A — Keep all current defaults.**
- **B — Keep `Features=strict`; require explicit unsafe and language version.**
- **C — Pin language version to the repository's SDK generation and disallow
  unsafe by default.**

### Recommendation

**B.** Unsafe is a capability, not a quality rule. `LangVersion=latest` changes
with installed SDKs and should not silently change consumer syntax.

**Requested answer:** A, B, or C, with any project categories that require
unsafe code.

## SDK-12 — Lock files and restore mode

**Question:** Should every project generate `packages.lock.json` and enforce it
on CI?

### Solution alternatives

- **A — Yes for every project**, matching current behavior.
- **B — Only applications/tools, not libraries.**
- **C — Leave lock-file policy to each repository.**

### Recommendation

**A** for reproducibility and current compatibility. Document the required lock
file changes whenever the SDK updates implicit dependencies.

**Requested answer:** A, B, or C.

## SDK-13 — MTP runtime defaults

**Question:** Which generic MTP behaviors are mandatory?

### Solution alternatives

- **A — Full profile:** TRX, CI report, coverage on CI, mini crash dumps, a
  ten-minute mini hang dump, retry/hot-reload extensions, and at least one test.
- **B — Diagnostic minimum:** TRX, crash/hang dumps, and at least one test;
  coverage, retry, hot reload, and CI report are opt-ins.
- **C — Packages only; no command-line defaults.**

### Recommendation

**B.** Empty-run protection and diagnostics prevent false-green or
non-actionable CI. Coverage/reporting are CI-specific; retry can conceal flaky
tests; hot reload is local tooling.

**Requested answer:** A, B, or C. If dumps are enabled, confirm timeout, dump
type, retention, and CI artifact handling.

## SDK-14 — Analyzer suppression during `dotnet test`

**Question:** May the MTP test build disable analyzers for speed?

### Solution alternatives

- **A — Yes by default**, following Meziantou.
- **B — Only when `ContinuousIntegrationBuild=true` and a separate analyzed
  build is declared.**
- **C — Never.**

### Recommendation

**B**, but only if CI can prove the separate build ran. Otherwise C. A can make
`dotnet test` the only local/CI command and silently bypass quality checks.

**Requested answer:** A, B, or C, and identify the guaranteed analyzed CI
command.

## SDK-15 — Application settings files

**Question:** Should the SDK alter copy/publish behavior for
`appsettings*.json`?

### Solution alternatives

- **A — Preserve current behavior for every project.**
- **B — Apply only to an explicit web/application profile.**
- **C — Exclude; applications own settings publication.**

### Recommendation

**B**, after tests prove environment-specific settings must never publish.
Library and generator projects should not receive application content rules.

**Requested answer:** A, B, or C. Confirm the intended handling of
`appsettings.json`, `appsettings.Production.json`, and other environment files.

## SDK-16 — Reqnroll settings

**Question:** Are Reqnroll code-behind and JSON defaults part of every test
project?

### Solution alternatives

- **A — Every test profile.**
- **B — Explicit Reqnroll profile only.**
- **C — Exclude from the SDK.**

### Recommendation

**B.** They are irrelevant to non-BDD tests and couple the baseline to one
framework.

**Requested answer:** A, B, or C.

## SDK-17 — SQL projects

**Question:** How should SQL projects participate?

### Solution alternatives

- **A — General SDK with automatic `UsingMicrosoftBuildSqlSdk` exclusions and
  SQL warning/code-analysis defaults.**
- **B — Explicit SQL profile.**
- **C — Unsupported in the first release.**

### Recommendation

**B.** SQL evaluation differs from C# and must not accidentally restore Roslyn
or MTP packages. Support requires a fixture using the actual SQL SDK in use.

**Requested answer:** A, B, or C, and identify all SQL SDKs/versions that must
work.

## SDK-18 — Package metadata and exact dependency versions

**Question:** Should the SDK standardize pack output?

### Solution alternatives

- **A — Full ARK pack profile:** author/license/icon/source/symbol metadata and
  exact project-reference dependency versions.
- **B — Safe public defaults only:** SourceLink, symbols, and validation;
  repositories own identity/license/version constraints.
- **C — No packaging behavior.**

### Recommendation

**B**, with A as an explicit organization profile. Exact dependency versions
are a package compatibility policy and need separate acceptance.

**Requested answer:** A, B, or C. Confirm whether project-reference versions
must remain exact (`[x.y.z]`) or use NuGet's normal lower-bound dependency.

## SDK-19 — SBOM, SourceLink, and Polyfill

**Question:** Which build packages are baseline implicit dependencies?

### Solution alternatives

- **A — All three.**
- **B — SourceLink and SBOM for pack/publish; Polyfill only for libraries that
  opt in.**
- **C — All explicit consumer references.**

### Recommendation

**B.** Source provenance and SBOM are output concerns. Polyfill changes
generated source and is not needed by every application/test project.

**Requested answer:** A, B, or C. Confirm whether SBOM is required for packages,
applications, containers, or all three.

## SDK-20 — Analyzer list

**Question:** Which current analyzers ship in the baseline?

### Solution alternatives

- **A — All current analyzers:** .NET, Banned API, Meziantou, VS Threading, and
  ErrorProne.
- **B — All except ErrorProne**, retaining it as opt-in.
- **C — .NET and Banned API only; all others opt in.**

### Recommendation

**B.** It preserves the mature configurations while isolating the old
ErrorProne package and its overlap with MA/VSTHRD rules.

**Requested answer:** A, B, or C. If A, confirm continued ownership of
ErrorProne.NET 0.1.2.

## SDK-21 — Banned symbols extensibility

**Question:** How do repositories add exceptions and additional bans?

### Solution alternatives

- **A — Immutable SDK list with one switch disabling all defaults.**
- **B — SDK list plus consumer `AdditionalFiles`; exceptions use analyzer
  suppression with justification.**
- **C — Copy the SDK list into each repository for editing.**

### Recommendation

**B.** It keeps updates centralized without preventing repository-specific
rules. Do not build a custom merge format before the analyzer requires one.

**Requested answer:** A, B, or C, and whether any current banned symbol needs a
global exception.

## SDK-22 — Release and compatibility contract

**Question:** What changes require an SDK major version?

### Solution alternatives

- **A — Semantic product contract:** new errors, bans, mandatory properties, or
  implicit package major upgrades are breaking.
- **B — Build-tool contract:** analyzer/default changes may ship in minor
  versions even when builds begin failing.
- **C — Calendar versioning with no compatibility promise.**

### Recommendation

**A.** A standards package can break builds without changing runtime APIs;
those failures still require controlled rollout and release notes.

**Requested answer:** A, B, or C, plus the desired preview/stable release
channels.

## SDK-23 — Migration strategy

**Question:** How should ReferenceProject prove adoption?

### Solution alternatives

- **A — Big-bang replacement of all copied files.**
- **B — Category-by-category migration with evaluated-project and build
  snapshots.**
- **C — Keep copied files permanently as a fallback.**

### Recommendation

**B.** Move analyzer packages/config first, then general properties, test
profile, content behavior, and packaging. Each stage must detect duplicate
imports and compare effective properties/items.

**Requested answer:** A, B, or C. Identify external consumer repositories, if
any, that may be used as compatibility fixtures.
