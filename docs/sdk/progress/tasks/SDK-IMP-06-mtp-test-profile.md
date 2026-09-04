# SDK-IMP-06 — Framework-neutral MTP test profile

**Category**: testing · **Priority**: productization
**Depends on**: SDK-IMP-04
**Scope**: SDK TEST PROFILE + MTP PACKAGES + TESTS
**Design**: [MTP baseline and framework ownership](../decisions.md#sdk-05--mtp-baseline-and-framework-ownership),
[Relevant MTP capabilities](../../design.md#relevant-mtp-capabilities)

## Problem

Test projects require a consistent Microsoft.Testing.Platform extension set and
safe defaults, but the SDK must not choose MSTest, Reqnroll, an assertion
library, or VSTest compatibility for the consumer.

## Execution map

- **Classification input**: consume the explicit-first `IsTestProject` result
  established by SDK-IMP-04; do not reimplement suffix detection.
- **Topology**: for tests set when empty `IsPackable=false`,
  `WarnOnPackingNonPackableProject=false`, `OutputType=Exe`, and
  `ExcludeByAttribute=Obsolete,GeneratedCodeAttribute`. Do not set
  `EnableMSTestRunner`.
- **Extensions**: inject exact implicit references to CrashDump, CodeCoverage,
  HangDump, HotReload, Retry, TrxReport, and AzureDevOpsReport. Give every
  extension an independent switch.
- **Command defaults**: behind `EnableArkToolsDefaultTestSettings`, append one
  TRX report, mini crash dump, mini hang dump with a ten-minute timeout,
  CI-selected coverage, and `--minimum-expected-tests 1`. A value of `0`
  suppresses the minimum-test argument.
- **Package-defined behavior**: HotReload, Retry, and AzureDevOpsReport are
  installed by default; do not invent undocumented command-line arguments for
  them.
- **Optimization**: before `_MTPBuild`, set `RunAnalyzers=false` unless
  `OptimizeTestRun=false`.
- **Ownership boundary**: inject no MSTest package, Reqnroll package,
  AwesomeAssertions package, or `Microsoft.NET.Test.Sdk`. Consumer `global.json`
  selects `"runner": "Microsoft.Testing.Platform"`.
- **Guide contribution**: create `docs/sdk/mtp.md` with every MTP extension,
  switch, command default, dump type, ten-minute hang timeout, coverage
  condition, minimum-test behavior, analyzer optimization, and CI artifact
  publication responsibility. SDK-IMP-10 links and reviews this task-owned
  guide; it does not rewrite it.

## Implementation steps

1. Implement test topology from SDK-IMP-04 classification after primary SDK
   defaults are available.
2. Add the seven exact extension references and per-extension switches.
3. Compose command-line arguments without duplicating consumer arguments or
   adding an invalid zero minimum.
4. Add `_MTPBuild` analyzer optimization and its opt-out.
5. Add one consumer-owned plain MSTest fixture and one consumer-owned
   Reqnroll.MsTest fixture; both select MTP in fixture `global.json`.
6. Add a no-test fixture that demonstrates the false-green guard.

## Required test coverage

- Projects classified as tests by SDK-IMP-04 receive the profile; an explicit
  non-test fixture receives none of it.
- Every extension resolves at the SDK-owned version and each opt-out removes
  only that extension.
- Command-line arguments contain each selected default once and preserve
  consumer additions.
- Coverage defaults on only in CI unless explicitly enabled or disabled.
- Minimum expected tests defaults to one; zero suppresses the argument; a
  no-test run fails under the default.
- `_MTPBuild` disables analyzers by default and `OptimizeTestRun=false` restores
  them.
- Plain MSTest and Reqnroll.MsTest discover tests without any framework,
  Reqnroll, assertion, or VSTest package injected by Ark.

## Outcomes

- Test projects receive the complete accepted MTP platform profile.
- Test framework, BDD adapter, assertion library, and runner selection remain
  consumer-owned.
- False-green empty test runs fail by default.

## Acceptance

- [x] Classification and topology follow explicit-first semantics.
- [x] All seven MTP extensions and every switch are tested.
- [x] TRX, coverage, dump, hang timeout, and minimum-test defaults are proven.
- [x] Analyzer optimization and its opt-out are proven.
- [x] MTP switches, dump settings, and CI artifact responsibility are
  documented.
- [x] Package graphs prove Ark injects no framework/assertion/Reqnroll/VSTest
  dependency.
- [x] The [task board](README.md) status for SDK-IMP-06 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
