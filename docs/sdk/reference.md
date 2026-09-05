# Ark.Tools SDK capability reference

The Ark.Tools SDK is an additional MSBuild SDK for .NET 8 and .NET 10
solutions. It applies the repository's build, analyzer, packaging, content,
and Microsoft Testing Platform (MTP) defaults without selecting a target
framework or test framework.

## Quick start

Pin the SDK once in `global.json`:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature"
  },
  "msbuild-sdks": {
    "Ark.Tools.Sdk": "6.6.6"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Add it alongside the primary project SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
```

The SDK injects the matching `Ark.Tools.Build` package. Its build assets flow
to downstream package consumers, but SDK-only package references and test
behavior do not. Add the SDK to every project that needs the complete profile.

Set `EnableArkToolsBuild=false` before SDK props are evaluated to disable the
entire profile. Individual capabilities can be disabled with the switches in
the tables below. Defaults are set only when the consumer has not supplied a
value unless stated otherwise.

## Capability overview

| Capability | Applies to | Main controls |
| --- | --- | --- |
| Build safety | C# and SQL projects | Nullable, implicit usings, warning policy, strict features, XML docs, SQL analysis |
| Analyzer configuration | Non-SQL C# projects | Packaged editorconfig/globalconfig files and banned symbols |
| Restore and audit | SDK-enabled projects | Lock files, locked CI restore, serialized globals, NuGet Audit |
| Tooling packages | SDK-enabled projects | Polyfill, SBOM, analyzers, and private exact versions |
| Packaging | Packable non-test projects | Package validation, symbols, `.snupkg` symbols |
| Test execution | Detected test projects | MTP extensions, executable output, empty-run protection, test diagnostics |
| Content | Test and application projects | `appsettings*.json`, `reqnroll*.json`, and `testconfig.json` output/publish metadata |
| IDE and source control | Supported .NET SDK projects | Visual Studio acceleration and the Copilot SourceLink workaround |

The focused references are [analyzer configuration and build policy](progress/tasks/SDK-IMP-03-analyzer-configuration-assets.md),
[restore and analyzers](progress/tasks/SDK-IMP-04-sdk-restore-and-analyzers.md),
[source and packaging](progress/tasks/SDK-IMP-05-source-and-packaging-profile.md),
[MTP](mtp.md), [content and Reqnroll](progress/tasks/SDK-IMP-07-content-and-reqnroll-profile.md),
and [ReferenceProject adoption](progress/tasks/SDK-IMP-09-reference-project-migration.md).

## Property reference

The following tables describe every non-private property set or consumed by
the packed SDK/build assets. “Early” means SDK props evaluation, before the
project body; “Build props” means the transitive package props import; “targets”
means the late target/item evaluation. A consumer can override a default
directly unless the table says that a switch controls the capability.

### Profile and build properties

| Property | Default or condition | Evaluation | Direct override |
| --- | --- | --- | --- |
| `EnableArkToolsBuild` | Enabled unless `false` | Early and package imports | Set `false` before SDK/NuGet props |
| `ArkToolsBuildImported` | `true` once; import guard | Build props | Do not override |
| `ArkToolsBuildImportCount` | `1` on the first import | Build props | Do not override |
| `TreatWarningsAsErrors` | `true` when empty | Build props | Set a value in the project |
| `MSBuildTreatWarningsAsErrors` | `true` when empty | Build props | Set a value in the project |
| `Nullable` | `enable` when empty for non-SQL C# | Build props | Set a value in the project |
| `ImplicitUsings` | `enable` when empty for non-SQL C# | Build props | Set `disable` or another value |
| `GenerateDocumentationFile` | `true` when empty for non-SQL C# | Build props | Set `false` or another value |
| `Features` | `strict` when empty for non-SQL C# | Build props | Set a value in the project |
| `ReportAnalyzer` | `true` when empty for non-SQL C# | Build props | Set `false` or another value |
| `EnforceCodeStyleInBuild` | `true` when empty for non-SQL C# | Build props | Set `false` or another value |
| `TreatTSqlWarningsAsErrors` | `true` when empty and `UsingMicrosoftBuildSqlSdk=true` | Build props | Set `false` or another value |
| `RunSqlCodeAnalysis` | `true` when empty and `UsingMicrosoftBuildSqlSdk=true` | Build props | Set `false` or another value |
| `EnableArkToolsCodingStyle` | Enabled unless `false` | Build props | Set `false` |
| `EnableArkToolsNetAnalyzers` | Enabled unless `false` | Build props and SDK restore | Set `false` |
| `EnableArkToolsMeziantouAnalyzer` | Enabled unless `false` | Build props and SDK restore | Set `false` |
| `EnableArkToolsErrorProne` | Enabled unless `false` | Build props and SDK restore | Set `false` |
| `EnableArkToolsVisualStudioThreading` | Disabled unless `true` | Build props and SDK restore | Set `true` |
| `EnableArkToolsIdentityModelConfiguration` | Enabled unless `false` | Build props | Set `false` |
| `EnableArkToolsCoreConfiguration` | Enabled unless `false` | Build props | Set `false` |
| `EnableArkToolsBannedApi` | Enabled unless `false` | Build targets and SDK restore | Set `false` |
| `EnableArkToolsSponsorLinkRemoval` | Enabled unless `false` | Before `CoreCompile` | Set `false` |
| `EnableArkToolsGlobalUsings` | Enabled unless `false` when implicit usings are enabled | Build targets | Set `false` |
| `GitTagVersion` | Valid semantic version derived from CI tag variables | Targets | Set a value directly |

`ArkToolsBuildImported` and `ArkToolsBuildImportCount` are diagnostics for
duplicate-import detection, not consumer configuration points. The SDK
standard analyzer configuration is inert when its corresponding analyzer is
absent. Consumer `AdditionalFiles` and explicitly supplied global configuration files
are combined with, rather than replaced by, Ark.Tools assets.

### Restore, compiler, and packaging properties

| Property | Default or condition | Evaluation | Direct override |
| --- | --- | --- | --- |
| `ContinuousIntegrationBuild` | `true` when a supported CI environment is detected and the value is empty | Early | Set a value directly |
| `RestorePackagesWithLockFile` | `true` when empty | Early | Set `false` |
| `RestoreSerializeGlobalProperties` | `true` when empty | Early | Set `false` |
| `RestoreLockedMode` | `true` when empty and `ContinuousIntegrationBuild=true` | Early | Set `false` |
| `RestoreUseStaticGraphEvaluation` | `false` when empty | Early | Set a value directly |
| `NuGetAudit` | `true` when empty | Early | Set `false` |
| `NuGetAuditMode` | `all` when empty | Early | Set `direct` or another supported value |
| `NuGetAuditLevel` | `low` when empty | Early | Set another supported level |
| `IsTestProject` | Explicit value first; otherwise `true` for names ending `.Tests` or `.UnitTests` | Early | Set `true` or `false` |
| `AnalysisLevel` | `latest-all` when empty for non-SQL C# | Early | Set a supported level |
| `LangVersion` | `14.0` when empty for non-SQL C# | Early | Set a supported language version |
| `PublishRepositoryUrl` | `true` when empty | Early | Set `false` or another value |
| `PolyUseEmbeddedAttribute` | `true` when empty unless `EnableArkToolsPolyfill=false` | Early | Set a value or disable Polyfill |
| `AccelerateBuildsInVisualStudio` | `true` when empty for .NET, Web, and Razor primary SDKs | Early | Set a value directly |
| `EnablePackageValidation` | `true` when empty for packable non-test projects | Early | Set `false` or another value |
| `IncludeSymbols` | `true` when empty for packable non-test projects | Early | Set `false` or another value |
| `SymbolPackageFormat` | `snupkg` when empty for packable non-test projects | Early | Set another format |
| `GenerateSBOM` | `true` when empty for non-SQL C# unless `EnableArkToolsSbom=false` | Early | Set `false` or disable SBOM |
| `EnableSourceControlManagerQueries` | `false` when `COPILOT_AGENT_ACTION` is set | Early | Set `EnableArkToolsCopilotSandboxWorkaround=false` or a direct value |
| `EnableSourceLink` | `false` when `COPILOT_AGENT_ACTION` is set | Early | Set `EnableArkToolsCopilotSandboxWorkaround=false` or a direct value |
| `EnableArkToolsSbom` | Enabled unless `false` | Early package injection | Set `false` |
| `EnableArkToolsPolyfill` | Enabled unless `false` | Early package injection | Set `false` |
| `EnableArkToolsCopilotSandboxWorkaround` | Enabled unless `false` when the environment signal exists | Early | Set `false` |

`Ark.Tools.Sdk` owns the exact versions of its implicit package references.
Consumers using Central Package Management must not add `PackageVersion`
entries for those implicit packages.

### Test and content properties

| Property | Default or condition | Evaluation | Direct override |
| --- | --- | --- | --- |
| `IsPackable` | `false` for test projects | Test targets | Set a value directly |
| `WarnOnPackingNonPackableProject` | `false` for test projects | Test targets | Set a value directly |
| `OutputType` | `Exe` when empty for test projects | SDK/test targets | Set a value directly |
| `TestingPlatformDotnetTestSupport` | `true` when empty for test projects | Early | Set a value directly |
| `ExcludeByAttribute` | `Obsolete,GeneratedCodeAttribute` for test projects | Test targets | Set a value directly |
| `MinimumExpectedTests` | `1` when empty and default test settings are enabled | Test targets | Set `0` to suppress the argument or another value |
| `TestingPlatformCommandLineArguments` | Composed from enabled MTP options when empty | Test targets | Supply the complete argument string |
| `OptimizeTestRun` | Analyzer suppression is enabled unless `false` | Before `_MTPBuild` | Set `false` |
| `EnableArkToolsMtpTestProfile` | Enabled unless `false` for test projects | Test targets and SDK restore | Set `false` |
| `EnableArkToolsDefaultTestSettings` | Enabled unless `false` | Test targets | Set `false` |
| `EnableArkToolsMtpCrashDump` | Enabled unless `false` | SDK restore and test arguments | Set `false` |
| `EnableArkToolsMtpCodeCoverage` | Enabled unless `false`; command argument only on CI | SDK restore and test arguments | Set `false` |
| `EnableArkToolsMtpHangDump` | Enabled unless `false` | SDK restore and test arguments | Set `false` |
| `EnableArkToolsMtpHotReload` | Enabled unless `false` | SDK restore | Set `false` |
| `EnableArkToolsMtpRetry` | Enabled unless `false` | SDK restore | Set `false` |
| `EnableArkToolsMtpTrxReport` | Enabled unless `false` | SDK restore and test arguments | Set `false` |
| `EnableArkToolsMtpAzureDevOpsReport` | Enabled unless `false` | SDK restore | Set `false` |
| `EnableArkToolsReqnroll` | Enabled unless `false` for test projects | Test targets | Set `false` |
| `ReqnrollUseIntermediateOutputPathForCodeBehind` | `true` when empty for test projects | Test targets | Set a value directly |
| `ReqnrollDeleteObsoleteCodeBehindFilesOnClean` | `true` when empty for test projects | Test targets | Set a value directly |
| `EnableArkToolsTestConfig` | Enabled unless `false` for test projects | Test targets | Set `false` |
| `EnableArkToolsAppSettings` | Enabled unless `false` | SDK targets | Set `false` |

The default test command enables TRX, mini crash dumps, mini hang dumps with a
10-minute timeout, and `--minimum-expected-tests 1`. CI also enables
`--coverage` when code coverage is enabled. The pipeline must publish the
generated TRX, dump, and coverage artifacts.

## Package capabilities

`Ark.Tools.Sdk` injects these private, exact package references:

| Package | Version | Condition |
| --- | --- | --- |
| `Ark.Tools.Build` | SDK-matched | SDK enabled |
| `Microsoft.CodeAnalysis.NetAnalyzers` | `10.0.400` | Non-SQL and enabled |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `4.14.0` | Non-SQL and enabled |
| `Meziantou.Analyzer` | `3.0.160` | Non-SQL and enabled |
| `Microsoft.VisualStudio.Threading.Analyzers` | `18.7.23` | Non-SQL and enabled |
| `ErrorProne.NET.CoreAnalyzers` | `0.1.2` | Non-SQL and enabled |
| `Microsoft.Sbom.Targets` | `4.1.5` | Non-SQL and SBOM enabled |
| `Polyfill` | `11.2.0` | Non-SQL and Polyfill enabled |
| `Microsoft.Testing.Extensions.CrashDump` | `2.3.3` | Test and enabled |
| `Microsoft.Testing.Extensions.CodeCoverage` | `18.10.0` | Test and enabled |
| `Microsoft.Testing.Extensions.HangDump` | `2.3.3` | Test and enabled |
| `Microsoft.Testing.Extensions.HotReload` | `2.3.3` | Test and enabled |
| `Microsoft.Testing.Extensions.Retry` | `2.3.3` | Test and enabled |
| `Microsoft.Testing.Extensions.TrxReport` | `2.3.3` | Test and enabled |
| `Microsoft.Testing.Extensions.AzureDevOpsReport` | `2.3.3` | Test and enabled |

The SDK intentionally does not add a test framework, assertion library,
Reqnroll adapter, `Microsoft.NET.Test.Sdk`, or VSTest compatibility bridge.
Those choices remain consumer-owned.

## Adoption and validation

1. Add the versionless SDK entry and MTP runner selection to `global.json`.
2. Add `<Sdk Name="Ark.Tools.Sdk" />` to each project that needs SDK-only
   behavior.
3. Remove Central Package Management entries for SDK-owned implicit packages.
4. Keep target frameworks, test frameworks, assertions, and project identity
   in consumer files.
5. Run restore once to create lock files, then use locked restore in CI.
6. Override a default directly in the project, or use the narrowest matching
   `EnableArkTools*` switch.

Build and test the solution with:

```bash
dotnet restore Ark.Tools.slnx
dotnet build Ark.Tools.slnx --no-restore
dotnet test Ark.Tools.slnx --no-build --minimum-expected-tests 1
```

The clean-consumer coverage is in
[`tests/Ark.Tools.Sdk.Tests/SdkPackageTests.cs`](../../tests/Ark.Tools.Sdk.Tests/SdkPackageTests.cs).
It validates package contents, evaluated properties/items, opt-outs, lock
files, analyzer behavior, and MTP execution. The package does not copy
configuration files into consumer repositories.
