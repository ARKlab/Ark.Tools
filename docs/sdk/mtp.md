# MTP test profile

Ark.Tools applies a framework-neutral Microsoft Testing Platform (MTP) profile to projects classified as tests. The SDK adds only the transport/runtime extensions and default command-line settings; it does not inject MSTest, Reqnroll, `Microsoft.NET.Test.Sdk`, or any assertion library.

## Activation and scope

- The profile applies only when `IsTestProject` is `true`.
- The profile is disabled by setting `EnableArkToolsMtpTestProfile=false`.
- Default test settings are disabled by setting `EnableArkToolsDefaultTestSettings=false`.
- Individual extension packages can be disabled with the matching `EnableArkToolsMtp*` switches.

## Test invocation

The repository opts into native MTP through `global.json`. With the .NET 10 SDK, use the native target selectors when targeting a specific project or solution:

```powershell
dotnet test --project .\Core\Ark.Reference.Core.Tests\Ark.Reference.Core.Tests.csproj
dotnet test --solution .\Ark.Reference.slnx
```

Running `dotnet test` from `samples\Ark.ReferenceProject` still executes the nested `Ark.Reference.slnx`. The nested solution and its `Directory.Build.*` files are intentional: the sample is an ejectable starter solution and must remain independently buildable.

`--list-tests` is a test-application option. In the current .NET 10 SDK/MTP combination, invoking it through the implicit or explicit solution target reports zero tests even though normal execution succeeds. List the reference project's tests by selecting the project explicitly:

```powershell
dotnet test --project .\Core\Ark.Reference.Core.Tests\Ark.Reference.Core.Tests.csproj --list-tests
```

This reports the 48 generated Reqnroll tests. Therefore, a zero result from `dotnet test --list-tests` at the sample directory or solution level is not evidence that the nested test project is missing or undiscoverable; use normal execution or an explicit project target to validate it.

## Installed extensions

The SDK injects these exact implicit package references for test projects:

- `Microsoft.Testing.Extensions.CrashDump` (`EnableArkToolsMtpCrashDump`)
- `Microsoft.Testing.Extensions.CodeCoverage` (`EnableArkToolsMtpCodeCoverage`)
- `Microsoft.Testing.Extensions.HangDump` (`EnableArkToolsMtpHangDump`)
- `Microsoft.Testing.Extensions.HotReload` (`EnableArkToolsMtpHotReload`)
- `Microsoft.Testing.Extensions.Retry` (`EnableArkToolsMtpRetry`)
- `Microsoft.Testing.Extensions.TrxReport` (`EnableArkToolsMtpTrxReport`)
- `Microsoft.Testing.Extensions.AzureDevOpsReport` (`EnableArkToolsMtpAzureDevOpsReport`)

These references are version-owned by the SDK and remain private to the consuming project.

## Default command settings

When the default profile is active, the SDK sets these defaults if the consumer has not already supplied a value:

- `IsPackable=false`
- `WarnOnPackingNonPackableProject=false`
- `OutputType=Exe`
- `ExcludeByAttribute=Obsolete,GeneratedCodeAttribute`
- `MinimumExpectedTests=1`
- `TestingPlatformCommandLineArguments=--report-trx --crashdump --crashdump-type mini --hangdump --hangdump-type mini --hangdump-timeout 10m ...`

The command line is composed as:

- `--report-trx`
- `--crashdump`
- `--crashdump-type mini`
- `--hangdump`
- `--hangdump-type mini`
- `--hangdump-timeout 10m`
- `--coverage --coverage-output-format cobertura` only when `ContinuousIntegrationBuild=true`
- `--minimum-expected-tests <value>` when `MinimumExpectedTests` is non-zero

A value of `0` for `MinimumExpectedTests` suppresses the explicit `--minimum-expected-tests` argument. This prevents false-green empty test runs by default.

## Analyzer optimization

Before `_MTPBuild`, the SDK sets `RunAnalyzers=false` unless the consumer sets `OptimizeTestRun=false`.

This preserves fast test runs while keeping analyzer execution available when a project explicitly opts out of the optimization.

## CI artifact responsibility

The default MTP command line produces TRX output, mini crash dumps, mini hang dumps, and, in CI, coverage output. The pipeline remains responsible for publishing the generated artifacts from the test run.

## Ownership boundary

Ark.Tools does not add or force:

- `MSTest.TestAdapter`
- `MSTest.TestFramework`
- `MSTest.Analyzers`
- `Microsoft.NET.Test.Sdk`
- `AwesomeAssertions`
- `Reqnroll.MsTest`
- any VSTest compatibility bridge

The consuming project is expected to choose its own test framework, BDD adapter, and assertion package and to select the runner in `global.json` when needed.
