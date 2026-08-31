// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Ark.Tools.Sdk.Tests;

/// <summary>
/// Verifies the foundation package archives and their clean-consumer assets.
/// </summary>
[TestClass]
public sealed class SdkPackageTests
{
    private static readonly string[] _selectedProperties =
    [
        "TreatWarningsAsErrors",
        "MSBuildTreatWarningsAsErrors",
        "Nullable",
        "ImplicitUsings",
        "GenerateDocumentationFile",
        "Features",
        "ReportAnalyzer",
        "EnforceCodeStyleInBuild",
        "TreatTSqlWarningsAsErrors",
        "RunSqlCodeAnalysis"
    ];

    private static readonly string[] _canonicalProperties =
    [
        "ArkToolsBuildImported",
        "ArkToolsBuildImportCount",
        "TreatWarningsAsErrors",
        "MSBuildTreatWarningsAsErrors",
        "Nullable",
        "ImplicitUsings",
        "GenerateDocumentationFile",
        "Features",
        "ReportAnalyzer",
        "EnforceCodeStyleInBuild",
        "TreatTSqlWarningsAsErrors",
        "RunSqlCodeAnalysis",
        "ArkToolsLocalAnalyzerConfigRoot",
        "ArkToolsLocalAnalyzerConfigRoot"
    ];

    private static readonly string[] _canonicalTargetProperties =
    [
        "ArkToolsBuildTargetsImported",
        "ArkToolsBuildImported",
        "ArkToolsBuildImportCount"
    ];

    private static readonly string[] _canonicalPropertyGroupLabels =
    [
        "Common Build Settings",
        "C# Build Settings",
        "SQL Build Settings",
        "Analyzer Configuration"
    ];

    private static readonly string[] _configurationAssets =
    [
        "configuration/coding-style/Ark.Tools.CodingStyle.editorconfig",
        "configuration/analyzers/Ark.Tools.NetAnalyzers.globalconfig",
        "configuration/analyzers/Ark.Tools.MeziantouAnalyzer.globalconfig",
        "configuration/analyzers/Ark.Tools.ErrorProne.globalconfig",
        "configuration/analyzers/Ark.Tools.VisualStudioThreading.globalconfig",
        "configuration/analyzers/Ark.Tools.IdentityModel.globalconfig",
        "configuration/analyzers/Ark.Tools.Core.globalconfig",
        "configuration/analyzers/Ark.Tools.BannedApi.BannedSymbols.txt"
    ];

    private static readonly string[] _globalConfigurationAssets =
    [
        "Ark.Tools.NetAnalyzers.globalconfig",
        "Ark.Tools.MeziantouAnalyzer.globalconfig",
        "Ark.Tools.ErrorProne.globalconfig",
        "Ark.Tools.VisualStudioThreading.globalconfig",
        "Ark.Tools.IdentityModel.globalconfig",
        "Ark.Tools.Core.globalconfig"
    ];

    private static readonly string[] _standardImplicitUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks"
    ];

    private static readonly string[] _buildPackageReference = ["Ark.Tools.Build"];

    private static readonly string[] _removedAnalyzerNames = ["DevLooped.SponsorLink", "Moq.CodeAnalysis"];

    private static readonly string[] _codingStyleAsset = ["Ark.Tools.CodingStyle.editorconfig"];

    private static readonly string[] _bannedApiAsset = ["BannedSymbols.Ark.Tools.txt"];

    private static readonly string[] _composedBannedApiAssets =
    [
        "BannedSymbols.Ark.Tools.txt",
        "BannedSymbols.Consumer.txt"
    ];

    private static readonly string[] _preservedAnalyzer = ["Preserved.Analyzer.dll"];

    private static readonly string[] _allSyntheticAnalyzers =
    [
        "DevLooped.SponsorLink.dll",
        "Moq.CodeAnalysis.dll",
        "Preserved.Analyzer.dll"
    ];

    private static readonly IReadOnlyDictionary<string, string> _sdkAnalyzerVersions =
        new Dictionary<string, string>
        {
            ["Microsoft.CodeAnalysis.NetAnalyzers"] = "10.0.400",
            ["Microsoft.CodeAnalysis.BannedApiAnalyzers"] = "4.14.0",
            ["Meziantou.Analyzer"] = "3.0.160",
            ["Microsoft.VisualStudio.Threading.Analyzers"] = "18.7.23",
            ["ErrorProne.NET.CoreAnalyzers"] = "0.1.2"
        };

    private static readonly string[] _excludedSdkPackages =
    [
        "AwesomeAssertions",
        "Microsoft.NET.Test.Sdk",
        "Reqnroll.MsTest",
        "MSTest.TestFramework",
        "xunit",
        "NUnit",
        "Microsoft.Testing.Extensions.CrashDump",
        "Microsoft.Testing.Extensions.CodeCoverage",
        "Microsoft.Testing.Extensions.HangDump",
        "Microsoft.Testing.Extensions.HotReload",
        "Microsoft.Testing.Extensions.Retry",
        "Microsoft.Testing.Extensions.TrxReport"
    ];

    private static readonly string[] _boundaryProperties =
    [
        "DebugType",
        "DebugSymbols",
        "Deterministic",
        "EmbedUntrackedSources",
        "EnableNETAnalyzers",
        "AnalysisLevel",
        "LangVersion",
        "TargetFramework",
        "TargetFrameworks",
        "IsTestProject",
        "IsPackable",
        "OutputType",
        "NoWarn",
        "AllowUnsafeBlocks",
        "GenerateSBOM",
        "PolyUseEmbeddedAttribute",
        "PublishSingleFile"
    ];

    private static readonly string[] _boundaryItems =
    [
        "PackageReference",
        "Content",
        "AdditionalFiles",
        "TestingPlatformBuilderHook",
        "ContentWithTargetPath",
        "FilesForPackagingFromProject"
    ];

    /// <summary>
    /// Ensures both package archives contain only their intended MSBuild assets.
    /// </summary>
    [TestMethod]
    public async Task PackageArchivesContainExpectedAssets()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var feed = Path.Join(root, "artifacts", "sdk-test-feed");
        Directory.CreateDirectory(feed);
        await _run("dotnet", $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion=999.9.9");
        await _run("dotnet", $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Sdk", "Ark.Tools.Sdk.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion=999.9.9");

        using var build = await ZipFile.OpenReadAsync(Path.Join(feed, "Ark.Tools.Build.999.9.9.nupkg"));
        using var sdk = await ZipFile.OpenReadAsync(Path.Join(feed, "Ark.Tools.Sdk.999.9.9.nupkg"));
        Assert.IsNotNull(build.GetEntry("build/Ark.Tools.Build.props"));
        Assert.IsNotNull(build.GetEntry("buildTransitive/Ark.Tools.Build.props"));
        foreach (var asset in _configurationAssets)
        {
            Assert.IsNotNull(build.GetEntry(asset), asset);
        }
        Assert.IsNull(build.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)));
        var sdkPropsEntry = sdk.GetEntry("Sdk/Sdk.props");
        Assert.IsNotNull(sdkPropsEntry);
        await using var sdkPropsStream = await sdkPropsEntry.OpenAsync();
        using var sdkPropsReader = new StreamReader(sdkPropsStream);
        var sdkProps = await sdkPropsReader.ReadToEndAsync().ConfigureAwait(false);
        StringAssert.Contains(sdkProps, "Version=\"999.9.9\"", StringComparison.Ordinal);
        StringAssert.Contains(sdkProps, "IsImplicitlyDefined=\"true\"", StringComparison.Ordinal);
        Assert.IsNull(sdk.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)));
        var nuspecEntry = build.GetEntry("Ark.Tools.Build.nuspec");
        Assert.IsNotNull(nuspecEntry);
        await using (var nuspecStream = await nuspecEntry.OpenAsync())
        using (var nuspec = new StreamReader(nuspecStream))
        {
            Assert.IsFalse((await nuspec.ReadToEndAsync().ConfigureAwait(false)).Contains("<dependencies>", StringComparison.Ordinal));
        }

        var consumer = Path.Join(root, "artifacts", "sdk-consumer");
        Directory.CreateDirectory(consumer);
        await File.WriteAllTextAsync(Path.Join(consumer, "Directory.Build.props"), "<Project><PropertyGroup><ArkToolsSdkProject>true</ArkToolsSdkProject><RestorePackagesWithLockFile>false</RestorePackagesWithLockFile><EnablePackageValidation>false</EnablePackageValidation></PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "Directory.Build.targets"), "<Project />").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "Directory.Packages.props"), "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "global.json"), """{"sdk":{"version":"10.0.400","rollForward":"latestFeature"},"msbuild-sdks":{"Ark.Tools.Sdk":"999.9.9"}}""").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "NuGet.Config"), $"<configuration><packageSources><clear /><add key=\"local\" value=\"{feed}\" /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources></configuration>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "Consumer.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
""");
        var packages = Path.Join(consumer, "packages");
        File.Delete(Path.Join(consumer, "packages.lock.json"));
        var environment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = packages,
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(consumer, "http-cache")
        };
        await _run("dotnet", $"restore \"{Path.Join(consumer, "Consumer.csproj")}\" --configfile \"{Path.Join(consumer, "NuGet.Config")}\"", environment);
        var properties = await _run("dotnet", $"msbuild \"{Path.Join(consumer, "Consumer.csproj")}\" -getProperty:ArkToolsBuildImported -getProperty:ArkToolsBuildImportCount", environment);
        StringAssert.Contains(properties, "\"ArkToolsBuildImported\": \"true\"", StringComparison.Ordinal);
        StringAssert.Contains(properties, "\"ArkToolsBuildImportCount\": \"1\"", StringComparison.Ordinal);

        var disabled = Path.Join(root, "artifacts", "sdk-consumer-disabled");
        Directory.CreateDirectory(disabled);
        File.Copy(Path.Join(consumer, "global.json"), Path.Join(disabled, "global.json"), true);
        File.Copy(Path.Join(consumer, "NuGet.Config"), Path.Join(disabled, "NuGet.Config"), true);
        File.Copy(Path.Join(consumer, "Directory.Build.props"), Path.Join(disabled, "Directory.Build.props"), true);
        File.Copy(Path.Join(consumer, "Directory.Packages.props"), Path.Join(disabled, "Directory.Packages.props"), true);
        File.Delete(Path.Join(disabled, "packages.lock.json"));
        await File.WriteAllTextAsync(Path.Join(disabled, "Consumer.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableArkToolsBuild>false</EnableArkToolsBuild>
  </PropertyGroup>
</Project>
""").ConfigureAwait(false);
        var disabledEnvironment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Join(disabled, "packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(disabled, "http-cache")
        };
        await _run("dotnet", $"restore \"{Path.Join(disabled, "Consumer.csproj")}\" --configfile \"{Path.Join(disabled, "NuGet.Config")}\"", disabledEnvironment);
        var disabledProperties = await _run("dotnet", $"msbuild \"{Path.Join(disabled, "Consumer.csproj")}\" -getProperty:ArkToolsBuildImported", disabledEnvironment);
        Assert.IsFalse(disabledProperties.Contains("\"ArkToolsBuildImported\": \"true\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures the canonical Build assets contain only the accepted public baseline.
    /// </summary>
    [TestMethod]
    public void CanonicalBuildAssetsContainOnlyAcceptedPolicy()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var buildRoot = Path.Join(root, "src", "sdk", "Ark.Tools.Build", "build");
        var props = XDocument.Load(Path.Join(buildRoot, "Ark.Tools.Build.common.props"));
        var targets = XDocument.Load(Path.Join(buildRoot, "Ark.Tools.Build.common.targets"));
        var rootProps = XDocument.Load(Path.Join(root, "Directory.Build.props"));
        var rootTargets = XDocument.Load(Path.Join(root, "Directory.Build.targets"));
        var sdkDirectoryProps = XDocument.Load(Path.Join(root, "src", "sdk", "Directory.Build.props"));
        var sdkDirectoryTargets = XDocument.Load(Path.Join(root, "src", "sdk", "Directory.Build.targets"));
        CollectionAssert.AreEqual(
            _canonicalProperties,
            props.Descendants("PropertyGroup").Elements().Select(element => element.Name.LocalName).ToArray());
        CollectionAssert.AreEqual(
            _canonicalPropertyGroupLabels,
            props.Descendants("PropertyGroup").Select(element => element.Attribute("Label")?.Value).ToArray());
        CollectionAssert.AreEqual(
            _canonicalTargetProperties,
            targets.Descendants("PropertyGroup").Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.AreEqual(8, props.Descendants("ItemGroup").Elements().Count());
        Assert.AreEqual(1, targets.Descendants("ItemGroup").Elements("AdditionalFiles").Count());
        var sponsorLinkTarget = targets.Descendants("Target").Single();
        Assert.AreEqual("Disable_SponsorLink", sponsorLinkTarget.Attribute("Name")?.Value);
        CollectionAssert.AreEquivalent(
            _removedAnalyzerNames,
            sponsorLinkTarget.Descendants("Analyzer")
                .Select(element => _removedAnalyzerNames.Single(name =>
                    element.Attribute("Condition")?.Value.Contains(name, StringComparison.Ordinal) == true))
                .ToArray());
        Assert.AreEqual(
            "$(MSBuildThisFileDirectory)src/sdk/Ark.Tools.Sdk/Sdk/Sdk.props",
            rootProps.Descendants("Import").Single().Attribute("Project")?.Value);
        Assert.AreEqual(
            "$(MSBuildThisFileDirectory)src/sdk/Ark.Tools.Sdk/Sdk/Sdk.targets",
            rootTargets.Descendants("Import").Single().Attribute("Project")?.Value);
        Assert.AreEqual(
            "true",
            sdkDirectoryProps.Descendants("ArkToolsSdkProject").Single().Value);
        Assert.AreEqual(
            "../../Directory.Build.props",
            sdkDirectoryProps.Descendants("Import").Single().Attribute("Project")?.Value);
        Assert.AreEqual(
            "../../Directory.Build.targets",
            sdkDirectoryTargets.Descendants("Import").Single().Attribute("Project")?.Value);
    }

    /// <summary>
    /// Ensures analyzer inventories and split diagnostic ownership match the accepted design.
    /// </summary>
    [TestMethod]
    public void AnalyzerConfigurationInventoriesMatchDesign()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var configurationRoot = Path.Join(root, "src", "sdk", "Ark.Tools.Build", "configuration", "analyzers");
        var files = new Dictionary<string, int>
        {
            ["Ark.Tools.NetAnalyzers.globalconfig"] = 97,
            ["Ark.Tools.MeziantouAnalyzer.globalconfig"] = 34,
            ["Ark.Tools.ErrorProne.globalconfig"] = 30,
            ["Ark.Tools.VisualStudioThreading.globalconfig"] = 23,
            ["Ark.Tools.IdentityModel.globalconfig"] = 1,
            ["Ark.Tools.Core.globalconfig"] = 1
        };

        foreach (var file in files)
        {
            var path = Path.Join(configurationRoot, file.Key);
            Assert.AreEqual(file.Value, _countConfiguredDiagnostics(path), file.Key);
            StringAssert.Contains(File.ReadAllText(path), "global_level = 90", StringComparison.Ordinal);
        }

        var bannedSymbols = File.ReadAllLines(Path.Join(configurationRoot, "BannedSymbols.Ark.Tools.txt"))
            .Count(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'));
        Assert.AreEqual(93, bannedSymbols);

        var ownershipFiles = Directory.GetFiles(configurationRoot, "*.globalconfig")
            .Append(Path.Join(root, ".editorconfig"))
            .ToArray();
        _assertDiagnosticOwner(ownershipFiles, "IDE1006", Path.Join(root, ".editorconfig"));
        _assertDiagnosticOwner(
            ownershipFiles,
            "IDX00001",
            Path.Join(configurationRoot, "Ark.Tools.IdentityModel.globalconfig"));
        _assertDiagnosticOwner(
            ownershipFiles,
            "ARKCORE005",
            Path.Join(configurationRoot, "Ark.Tools.Core.globalconfig"));
    }

    /// <summary>
    /// Ensures every packaged configuration asset is independently switchable and capability safe.
    /// </summary>
    [TestMethod]
    public async Task AnalyzerConfigurationAssetsAreSwitchableAndCapabilitySafe()
    {
        const string packageVersion = "999.9.11";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-configuration");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = Path.Join(fixtureRoot, "feed");
        Directory.CreateDirectory(feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");

        using var baseline = await _evaluateAsync(
            fixtureRoot,
            feed,
            "assets-baseline",
            "Consumer.csproj",
            _createCSharpProject(packageVersion));
        var editorConfigFiles = _getArkBuildItemFileNames(baseline, "EditorConfigFiles");
        CollectionAssert.Contains(editorConfigFiles, _codingStyleAsset[0]);
        Assert.AreEqual(1, editorConfigFiles.Count(file => file == _codingStyleAsset[0]));
        CollectionAssert.AreEquivalent(
            _globalConfigurationAssets,
            _getArkBuildItemFileNames(baseline, "GlobalAnalyzerConfigFiles"));
        CollectionAssert.AreEqual(
            _bannedApiAsset,
            _getArkBuildItemFileNames(baseline, "AdditionalFiles"));

        var switches = new Dictionary<string, string>
        {
            ["EnableArkToolsCodingStyle"] = "Ark.Tools.CodingStyle.editorconfig",
            ["EnableArkToolsNetAnalyzers"] = "Ark.Tools.NetAnalyzers.globalconfig",
            ["EnableArkToolsMeziantouAnalyzer"] = "Ark.Tools.MeziantouAnalyzer.globalconfig",
            ["EnableArkToolsErrorProne"] = "Ark.Tools.ErrorProne.globalconfig",
            ["EnableArkToolsVisualStudioThreading"] = "Ark.Tools.VisualStudioThreading.globalconfig",
            ["EnableArkToolsIdentityModelConfiguration"] = "Ark.Tools.IdentityModel.globalconfig",
            ["EnableArkToolsCoreConfiguration"] = "Ark.Tools.Core.globalconfig",
            ["EnableArkToolsBannedApi"] = "BannedSymbols.Ark.Tools.txt"
        };
        var baselineFiles = _getAllArkBuildConfigurationFileNames(baseline);
        foreach (var item in switches)
        {
            using var disabled = await _evaluateAsync(
                fixtureRoot,
                feed,
                $"disabled-{item.Key}",
                "Consumer.csproj",
                _createCSharpProject(packageVersion),
                $"<{item.Key}>false</{item.Key}>");
            CollectionAssert.AreEquivalent(
                baselineFiles.Where(file => !string.Equals(file, item.Value, StringComparison.Ordinal)).ToArray(),
                _getAllArkBuildConfigurationFileNames(disabled),
                item.Key);
        }

        var localConfigRoot = Path.Join(fixtureRoot, "local-config");
        Directory.CreateDirectory(localConfigRoot);
        var localConfig = Path.Join(localConfigRoot, ".consumer.globalconfig");
        await File.WriteAllTextAsync(localConfig, "is_global = true\nglobal_level = 100\ndotnet_diagnostic.CA1821.severity = none\n").ConfigureAwait(false);
        var localRootProperty = $"<ArkToolsLocalAnalyzerConfigRoot>{localConfigRoot}</ArkToolsLocalAnalyzerConfigRoot>";
        using var local = await _evaluateAsync(
            fixtureRoot,
            feed,
            "local-config-enabled",
            "Consumer.csproj",
            _createCSharpProject(packageVersion),
            localRootProperty);
        CollectionAssert.Contains(_getItemIdentities(local, "GlobalAnalyzerConfigFiles"), localConfig);
        using var localDisabled = await _evaluateAsync(
            fixtureRoot,
            feed,
            "local-config-disabled",
            "Consumer.csproj",
            _createCSharpProject(packageVersion),
            $"{localRootProperty}<EnableArkToolsLocalAnalyzerConfigDiscovery>false</EnableArkToolsLocalAnalyzerConfigDiscovery>");
        CollectionAssert.DoesNotContain(_getItemIdentities(localDisabled, "GlobalAnalyzerConfigFiles"), localConfig);

        using var sql = await _evaluateAsync(
            fixtureRoot,
            feed,
            "configuration-sql",
            "Consumer.sqlproj",
            _createSqlProject(packageVersion));
        Assert.AreEqual(0, _getAllArkBuildConfigurationFileNames(sql).Length);

        var scenarioRoot = Path.Join(fixtureRoot, "assets-baseline");
        await File.WriteAllTextAsync(Path.Join(scenarioRoot, "Consumer.cs"), "internal sealed class Consumer { }\n").ConfigureAwait(false);
        await _run("dotnet", $"build \"{Path.Join(scenarioRoot, "Consumer.csproj")}\" --no-restore", _createEnvironment(scenarioRoot));
        Assert.IsFalse(File.Exists(Path.Join(scenarioRoot, "Ark.Tools.CodingStyle.editorconfig")));
        Assert.IsFalse(File.Exists(Path.Join(scenarioRoot, "Ark.Tools.NetAnalyzers.globalconfig")));
    }

    /// <summary>
    /// Ensures compiler configuration precedence and packaged banned symbols work in consumer source.
    /// </summary>
    [TestMethod]
    public async Task CompilerConfigurationPrecedenceAndBannedApiAreEnforced()
    {
        const string packageVersion = "999.9.12";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-compiler");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = Path.Join(fixtureRoot, "feed");
        Directory.CreateDirectory(feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");

        var source = "internal sealed class Consumer { ~Consumer() { } }\n";
        var packagedRoot = await _createCompilerScenarioAsync(fixtureRoot, feed, "packaged", packageVersion, source);
        var packagedError = await _runForExitCode(
            "dotnet",
            $"build \"{Path.Join(packagedRoot, "Consumer.csproj")}\" --no-restore",
            _createEnvironment(packagedRoot));
        Assert.AreNotEqual(0, packagedError.ExitCode);
        StringAssert.Contains(packagedError.Output, "CA1821", StringComparison.Ordinal);

        var globalRoot = await _createCompilerScenarioAsync(fixtureRoot, feed, "local-global", packageVersion, source);
        await File.WriteAllTextAsync(
            Path.Join(globalRoot, ".globalconfig"),
            "is_global = true\ndotnet_diagnostic.CA1821.severity = none\n").ConfigureAwait(false);
        await _run("dotnet", $"build \"{Path.Join(globalRoot, "Consumer.csproj")}\" --no-restore", _createEnvironment(globalRoot));

        var editorRoot = await _createCompilerScenarioAsync(fixtureRoot, feed, "source-editor", packageVersion, source);
        await File.WriteAllTextAsync(
            Path.Join(editorRoot, ".globalconfig"),
            "is_global = true\ndotnet_diagnostic.CA1821.severity = none\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(editorRoot, ".editorconfig"),
            "root = true\n[*.cs]\ndotnet_diagnostic.CA1821.severity = error\n").ConfigureAwait(false);
        var editorError = await _runForExitCode(
            "dotnet",
            $"build \"{Path.Join(editorRoot, "Consumer.csproj")}\" --no-restore",
            _createEnvironment(editorRoot));
        Assert.AreNotEqual(0, editorError.ExitCode);
        StringAssert.Contains(editorError.Output, "CA1821", StringComparison.Ordinal);

        var nestedRoot = await _createCompilerScenarioAsync(fixtureRoot, feed, "nested-editor", packageVersion, "");
        var nested = Path.Join(nestedRoot, "Nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(
            Path.Join(nestedRoot, ".globalconfig"),
            "is_global = true\ndotnet_diagnostic.CA1821.severity = none\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(nestedRoot, ".editorconfig"),
            "root = true\n[*.cs]\ndotnet_diagnostic.CA1821.severity = error\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(nested, ".editorconfig"),
            "[*.cs]\ndotnet_diagnostic.CA1821.severity = none\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(nested, "Consumer.cs"), source).ConfigureAwait(false);
        await _run("dotnet", $"build \"{Path.Join(nestedRoot, "Consumer.csproj")}\" --no-restore", _createEnvironment(nestedRoot));

        var bannedProject = _createCSharpProject(
            packageVersion,
            "",
            """<PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="4.14.0" PrivateAssets="all" />""",
            """<AdditionalFiles Include="BannedSymbols.Consumer.txt" />""");
        using var banned = await _evaluateAsync(
            fixtureRoot,
            feed,
            "banned-api",
            "Consumer.csproj",
            bannedProject);
        var bannedRoot = Path.Join(fixtureRoot, "banned-api");
        await File.WriteAllTextAsync(
            Path.Join(bannedRoot, "Consumer.cs"),
            "internal static class Consumer { static Consumer() { _ = System.DateTime.Now; } }\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(bannedRoot, "BannedSymbols.Consumer.txt"),
            "P:System.DateTime.Today;Use an explicit timezone\n").ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            _composedBannedApiAssets,
            _getItemIdentities(banned, "AdditionalFiles").Select(identity => Path.GetFileName(identity) ?? "").ToArray());
        var bannedError = await _runForExitCode(
            "dotnet",
            $"build \"{Path.Join(bannedRoot, "Consumer.csproj")}\" --no-restore",
            _createEnvironment(bannedRoot));
        Assert.AreNotEqual(0, bannedError.ExitCode);
        StringAssert.Contains(bannedError.Output, "Consumer.cs", StringComparison.Ordinal);
        StringAssert.Contains(bannedError.Output, "RS0030", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures SDK restore, audit, compiler, CI, and test-classification policy is early and overrideable.
    /// </summary>
    [TestMethod]
    public async Task SdkPackagingProfileAddsPackageBackedToolingAndOptOuts()
    {
        const string packageVersion = "999.9.14";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-packaging-profile");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        using var baseline = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "baseline",
            "Consumer.csproj",
            _createSdkCSharpProject());
        var packageReferences = _getPackageReferences(baseline);
        Assert.AreEqual("11.2.0", packageReferences["Polyfill"]["Version"]);
        Assert.AreEqual("4.1.5", packageReferences["Microsoft.Sbom.Targets"]["Version"]);
        Assert.AreEqual("10.0.400", packageReferences["Microsoft.SourceLink.GitHub"]["Version"]);
        _assertProperties(baseline, new Dictionary<string, string>
        {
            ["GenerateSBOM"] = "true",
            ["PolyUseEmbeddedAttribute"] = "true",
            ["AccelerateBuildsInVisualStudio"] = "true",
            ["EnablePackageValidation"] = "true",
            ["IncludeSymbols"] = "true",
            ["SymbolPackageFormat"] = "snupkg"
        });
        CollectionAssert.Contains(_getItemIdentities(baseline, "Using"), "System.Diagnostics.CodeAnalysis");
        CollectionAssert.Contains(_getItemIdentities(baseline, "Using"), "System.Globalization");
        CollectionAssert.Contains(_getItemIdentities(baseline, "Using"), "System.Text");

        using var disabledPolyfill = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "disabled-polyfill",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsPolyfill>false</EnableArkToolsPolyfill>");
        Assert.IsFalse(_getPackageReferences(disabledPolyfill).ContainsKey("Polyfill"));
        Assert.AreEqual("", _getProperty(disabledPolyfill, "PolyUseEmbeddedAttribute"));

        using var disabledSbom = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "disabled-sbom",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsSbom>false</EnableArkToolsSbom>");
        Assert.IsFalse(_getPackageReferences(disabledSbom).ContainsKey("Microsoft.Sbom.Targets"));
        Assert.AreEqual("", _getProperty(disabledSbom, "GenerateSBOM"));

        using var disabledSourceLink = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "disabled-sourcelink",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsSourceLink>false</EnableArkToolsSourceLink>");
        Assert.IsFalse(_getPackageReferences(disabledSourceLink).ContainsKey("Microsoft.SourceLink.GitHub"));

        using var disabledGlobalUsings = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "disabled-global-usings",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsGlobalUsings>false</EnableArkToolsGlobalUsings>");
        CollectionAssert.DoesNotContain(_getItemIdentities(disabledGlobalUsings, "Using"), "System.Diagnostics.CodeAnalysis");

        var copilotEnvironment = _createSdkEnvironment(fixtureRoot);
        copilotEnvironment["COPILOT_AGENT_ACTION"] = "true";
        using var copilot = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "copilot",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            environment: copilotEnvironment);
        Assert.AreEqual("false", _getProperty(copilot, "EnableSourceControlManagerQueries"));
        Assert.AreEqual("false", _getProperty(copilot, "EnableSourceLink"));
        using var copilotOptOut = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "copilot-opt-out",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            environment: copilotEnvironment,
            directoryProperties: "<EnableArkToolsCopilotSandboxWorkaround>false</EnableArkToolsCopilotSandboxWorkaround>");
        Assert.AreEqual("true", _getProperty(copilotOptOut, "EnableSourceControlManagerQueries"));
        Assert.AreEqual("true", _getProperty(copilotOptOut, "EnableSourceLink"));

        using var sql = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "sql",
            "Consumer.sqlproj",
            _createSdkSqlProject());
        Assert.IsFalse(_getPackageReferences(sql).ContainsKey("Microsoft.Sbom.Targets"));
        Assert.AreEqual("", _getProperty(sql, "GenerateSBOM"));
    }

    /// <summary>
    /// Ensures SDK restore, audit, compiler, CI, and test-classification policy is early and overrideable.
    /// </summary>
    [TestMethod]
    public async Task SdkRestoreAndCompilerPolicyIsEarlyAndOverrideable()
    {
        const string packageVersion = "999.9.15";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-restore-policy");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        using var local = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "local",
            "Consumer.csproj",
            _createSdkCSharpProject());
        _assertProperties(local, new Dictionary<string, string>
        {
            ["_IsGitHubActions"] = "",
            ["ContinuousIntegrationBuild"] = "",
            ["RestorePackagesWithLockFile"] = "true",
            ["RestoreSerializeGlobalProperties"] = "true",
            ["RestoreLockedMode"] = "",
            ["NuGetAudit"] = "true",
            ["NuGetAuditMode"] = "all",
            ["NuGetAuditLevel"] = "low",
            ["AnalysisLevel"] = "latest-all",
            ["LangVersion"] = "14.0",
            ["IsTestProject"] = ""
        });
        var warningsNotAsErrors = _getProperty(local, "WarningsNotAsErrors");
        Assert.IsFalse(warningsNotAsErrors.Contains("NU1901", StringComparison.Ordinal));
        Assert.IsFalse(warningsNotAsErrors.Contains("NU1905", StringComparison.Ordinal));

        foreach (var signal in new[] { "TF_BUILD", "GITHUB_ACTIONS", "CI" })
        {
            var environment = _createSdkEnvironment(fixtureRoot);
            environment[signal] = "true";
            using var detected = await _evaluateSdkAsync(
                fixtureRoot,
                feed,
                $"ci-{signal}",
                "Consumer.csproj",
                _createSdkCSharpProject(),
                environment: environment);
            Assert.AreEqual("true", _getProperty(detected, "ContinuousIntegrationBuild"), signal);
            Assert.AreEqual("true", _getProperty(detected, "RestoreLockedMode"), signal);
            Assert.AreEqual(
                signal == "GITHUB_ACTIONS" ? "true" : "",
                _getProperty(detected, "_IsGitHubActions"),
                signal);
        }

        var explicitEnvironment = _createSdkEnvironment(fixtureRoot);
        explicitEnvironment["GITHUB_ACTIONS"] = "true";
        using var explicitCi = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "explicit-ci",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            environment: explicitEnvironment,
            directoryProperties: "<ContinuousIntegrationBuild>false</ContinuousIntegrationBuild>");
        _assertProperties(explicitCi, new Dictionary<string, string>
        {
            ["_IsGitHubActions"] = "true",
            ["ContinuousIntegrationBuild"] = "false",
            ["RestoreLockedMode"] = ""
        });

        var overrides = string.Join(
            Environment.NewLine,
            "<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>",
            "<RestoreSerializeGlobalProperties>false</RestoreSerializeGlobalProperties>",
            "<RestoreLockedMode>false</RestoreLockedMode>",
            "<NuGetAudit>false</NuGetAudit>",
            "<NuGetAuditMode>direct</NuGetAuditMode>",
            "<NuGetAuditLevel>high</NuGetAuditLevel>",
            "<AnalysisLevel>9.0</AnalysisLevel>",
            "<LangVersion>13.0</LangVersion>");
        using var overridden = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "overridden",
            "Consumer.csproj",
            _createSdkCSharpProject(overrides));
        _assertProperties(overridden, new Dictionary<string, string>
        {
            ["RestorePackagesWithLockFile"] = "false",
            ["RestoreSerializeGlobalProperties"] = "false",
            ["RestoreLockedMode"] = "false",
            ["NuGetAudit"] = "false",
            ["NuGetAuditMode"] = "direct",
            ["NuGetAuditLevel"] = "high",
            ["AnalysisLevel"] = "9.0",
            ["LangVersion"] = "13.0"
        });

        using var tests = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "suffix-tests",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject());
        Assert.AreEqual("true", _getProperty(tests, "IsTestProject"));
        using var unitTests = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "suffix-unit-tests",
            "Consumer.UnitTests.csproj",
            _createSdkCSharpProject());
        Assert.AreEqual("true", _getProperty(unitTests, "IsTestProject"));
        using var explicitFalse = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "suffix-explicit-false",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject("<IsTestProject>false</IsTestProject>"));
        Assert.AreEqual("false", _getProperty(explicitFalse, "IsTestProject"));
        using var explicitTrue = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "explicit-test",
            "Consumer.csproj",
            _createSdkCSharpProject("<IsTestProject>true</IsTestProject>"));
        Assert.AreEqual("true", _getProperty(explicitTrue, "IsTestProject"));
    }

    /// <summary>
    /// Ensures the SDK adds only the accepted MTP test extensions and default safety settings for test projects.
    /// </summary>
    [TestMethod]
    public async Task SdkTestProfileAddsFrameworkNeutralMtpExtensionsAndDefaults()
    {
        const string packageVersion = "999.9.17";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-mtp-profile");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        using var baseline = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "baseline",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject());
        var packageReferences = _getPackageReferences(baseline);
        foreach (var package in new[]
        {
            ("Microsoft.Testing.Extensions.CrashDump", "2.3.3"),
            ("Microsoft.Testing.Extensions.CodeCoverage", "18.10.0"),
            ("Microsoft.Testing.Extensions.HangDump", "2.3.3"),
            ("Microsoft.Testing.Extensions.HotReload", "2.3.3"),
            ("Microsoft.Testing.Extensions.Retry", "2.3.3"),
            ("Microsoft.Testing.Extensions.TrxReport", "2.3.3"),
            ("Microsoft.Testing.Extensions.AzureDevOpsReport", "2.3.3")
        })
        {
            Assert.AreEqual(package.Item2, packageReferences[package.Item1]["Version"], package.Item1);
            Assert.AreEqual("true", packageReferences[package.Item1]["IsImplicitlyDefined"], package.Item1);
        }
        Assert.AreEqual("false", _getProperty(baseline, "IsPackable"));
        Assert.AreEqual("false", _getProperty(baseline, "WarnOnPackingNonPackableProject"));
        Assert.AreEqual("Exe", _getProperty(baseline, "OutputType"));
        Assert.AreEqual("Obsolete,GeneratedCodeAttribute", _getProperty(baseline, "ExcludeByAttribute"));
        Assert.AreEqual("", _getProperty(baseline, "EnablePackageValidation"));
        Assert.AreEqual("", _getProperty(baseline, "IncludeSymbols"));
        Assert.AreEqual("1", _getProperty(baseline, "MinimumExpectedTests"));
        Assert.IsTrue(_getProperty(baseline, "TestingPlatformCommandLineArguments").Contains("--report-trx", StringComparison.Ordinal));
        Assert.IsTrue(_getProperty(baseline, "TestingPlatformCommandLineArguments").Contains("--minimum-expected-tests 1", StringComparison.Ordinal));
        Assert.IsFalse(packageReferences.ContainsKey("MSTest.TestFramework"));
        Assert.IsFalse(packageReferences.ContainsKey("Microsoft.NET.Test.Sdk"));
        Assert.IsFalse(packageReferences.ContainsKey("Reqnroll.MsTest"));

        var ciEnvironment = _createSdkEnvironment(fixtureRoot);
        ciEnvironment["CI"] = "true";
        using var ci = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "ci-defaults",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            environment: ciEnvironment);
        var ciArguments = _getProperty(ci, "TestingPlatformCommandLineArguments");
        Assert.IsTrue(ciArguments.Contains("--coverage", StringComparison.Ordinal));
        Assert.IsTrue(ciArguments.Contains("--coverage-output-format cobertura", StringComparison.Ordinal));

        using var disabled = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "disabled-mtp",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsMtpTestProfile>false</EnableArkToolsMtpTestProfile>");
        Assert.IsFalse(_getPackageReferences(disabled).ContainsKey("Microsoft.Testing.Extensions.CrashDump"));
        Assert.AreEqual("", _getProperty(disabled, "TestingPlatformCommandLineArguments"));

        using var optOut = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "opt-out-coverage",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            environment: ciEnvironment,
            directoryProperties: "<EnableArkToolsMtpCodeCoverage>false</EnableArkToolsMtpCodeCoverage>");
        Assert.IsFalse(_getPackageReferences(optOut).ContainsKey("Microsoft.Testing.Extensions.CodeCoverage"));

        using var nonTest = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "non-test",
            "Consumer.csproj",
            _createSdkCSharpProject());
        Assert.IsFalse(_getPackageReferences(nonTest).ContainsKey("Microsoft.Testing.Extensions.CrashDump"));
        Assert.AreEqual("", _getProperty(nonTest, "TestingPlatformCommandLineArguments"));
    }

    /// <summary>
    /// Ensures application settings and Reqnroll content semantics stay project-type aware and independently disableable.
    /// </summary>
    [TestMethod]
    public async Task SdkContentAndReqnrollProfileAppliesOnlyToDetectedTestProjects()
    {
        const string packageVersion = "999.9.18";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-content-reqnroll");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        var nonTestRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "non-test",
            "Consumer.csproj",
            _createSdkCSharpProject());
        await File.WriteAllTextAsync(Path.Join(nonTestRoot, "appsettings.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(nonTestRoot, "appsettings.Development.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(nonTestRoot, "reqnroll.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(nonTestRoot, "testconfig.json"), "{}\n").ConfigureAwait(false);
        var nonTestEvaluation = JsonDocument.Parse(await _run(
            "dotnet",
            $"msbuild \"{Path.Join(nonTestRoot, "Consumer.csproj")}\" -getProperty:ReqnrollUseIntermediateOutputPathForCodeBehind,ReqnrollDeleteObsoleteCodeBehindFilesOnClean -getItem:None,Content",
            _createSdkEnvironment(nonTestRoot)));
        Assert.AreEqual("", _getProperty(nonTestEvaluation, "ReqnrollUseIntermediateOutputPathForCodeBehind"));
        Assert.AreEqual("", _getProperty(nonTestEvaluation, "ReqnrollDeleteObsoleteCodeBehindFilesOnClean"));
        var nonTestReqnroll = _findItem(nonTestEvaluation, "None", "reqnroll.json");
        var nonTestTestConfig = _findItem(nonTestEvaluation, "None", "testconfig.json");
        Assert.IsFalse(nonTestReqnroll is not null && nonTestReqnroll.ContainsKey("CopyToOutputDirectory"));
        Assert.IsFalse(nonTestTestConfig is not null && nonTestTestConfig.ContainsKey("CopyToOutputDirectory"));

        var testRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "test-project",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject());
        await File.WriteAllTextAsync(Path.Join(testRoot, "appsettings.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(testRoot, "appsettings.Development.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(testRoot, "reqnroll.json"), "{}\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(testRoot, "testconfig.json"), "{}\n").ConfigureAwait(false);
        var testEvaluation = JsonDocument.Parse(await _run(
            "dotnet",
            $"msbuild \"{Path.Join(testRoot, "Consumer.Tests.csproj")}\" -getProperty:ReqnrollUseIntermediateOutputPathForCodeBehind,ReqnrollDeleteObsoleteCodeBehindFilesOnClean -getItem:None,Content",
            _createSdkEnvironment(testRoot)));
        Assert.AreEqual("true", _getProperty(testEvaluation, "ReqnrollUseIntermediateOutputPathForCodeBehind"));
        Assert.AreEqual("true", _getProperty(testEvaluation, "ReqnrollDeleteObsoleteCodeBehindFilesOnClean"));
        var noneItems = _getItemIdentities(testEvaluation, "None");
        var noneFileNames = noneItems.Select(Path.GetFileName).ToArray();
        var appsettingsBase = _findItem(testEvaluation, "None", "appsettings.json");
        var appsettingsEnvironment = _findItem(testEvaluation, "None", "appsettings.Development.json");
        var reqnroll = _findItem(testEvaluation, "None", "reqnroll.json");
        var testConfig = _findItem(testEvaluation, "None", "testconfig.json");
        Assert.IsNotNull(appsettingsBase);
        Assert.IsNotNull(appsettingsEnvironment);
        Assert.IsNotNull(reqnroll);
        Assert.IsNotNull(testConfig);
        Assert.AreEqual("Always", appsettingsBase!["CopyToOutputDirectory"]);
        Assert.AreEqual("Always", appsettingsBase["CopyToPublishDirectory"]);
        Assert.AreEqual("Always", appsettingsEnvironment!["CopyToOutputDirectory"]);
        Assert.AreEqual("Never", appsettingsEnvironment["CopyToPublishDirectory"]);
        Assert.AreEqual("Always", reqnroll!["CopyToOutputDirectory"]);
        Assert.AreEqual("PreserveNewest", testConfig!["CopyToOutputDirectory"]);
        Assert.AreEqual(1, noneFileNames.Count(file => string.Equals(file, "appsettings.json", StringComparison.Ordinal)));
        Assert.AreEqual(1, noneFileNames.Count(file => string.Equals(file, "appsettings.Development.json", StringComparison.Ordinal)));
        CollectionAssert.Contains(noneFileNames, "reqnroll.json");
        CollectionAssert.Contains(noneFileNames, "testconfig.json");

        var appSettingsDisabledRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "appsettings-disabled",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsAppSettings>false</EnableArkToolsAppSettings>");
        await File.WriteAllTextAsync(Path.Join(appSettingsDisabledRoot, "appsettings.json"), "{}\n").ConfigureAwait(false);
        var appSettingsDisabledEvaluation = JsonDocument.Parse(await _run(
            "dotnet",
            $"msbuild \"{Path.Join(appSettingsDisabledRoot, "Consumer.Tests.csproj")}\" -getProperty:ReqnrollUseIntermediateOutputPathForCodeBehind,ReqnrollDeleteObsoleteCodeBehindFilesOnClean -getItem:None,Content",
            _createSdkEnvironment(appSettingsDisabledRoot)));
        Assert.AreEqual("true", _getProperty(appSettingsDisabledEvaluation, "ReqnrollUseIntermediateOutputPathForCodeBehind"));
        Assert.AreEqual("true", _getProperty(appSettingsDisabledEvaluation, "ReqnrollDeleteObsoleteCodeBehindFilesOnClean"));

        var reqnrollDisabledRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "reqnroll-disabled",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsReqnroll>false</EnableArkToolsReqnroll>");
        await File.WriteAllTextAsync(Path.Join(reqnrollDisabledRoot, "reqnroll.json"), "{}\n").ConfigureAwait(false);
        var reqnrollDisabledEvaluation = JsonDocument.Parse(await _run(
            "dotnet",
            $"msbuild \"{Path.Join(reqnrollDisabledRoot, "Consumer.Tests.csproj")}\" -getProperty:ReqnrollUseIntermediateOutputPathForCodeBehind,ReqnrollDeleteObsoleteCodeBehindFilesOnClean -getItem:None,Content",
            _createSdkEnvironment(reqnrollDisabledRoot)));
        Assert.AreEqual("", _getProperty(reqnrollDisabledEvaluation, "ReqnrollUseIntermediateOutputPathForCodeBehind"));
        Assert.AreEqual("", _getProperty(reqnrollDisabledEvaluation, "ReqnrollDeleteObsoleteCodeBehindFilesOnClean"));

        var testConfigDisabledRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "testconfig-disabled",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsTestConfig>false</EnableArkToolsTestConfig>");
        await File.WriteAllTextAsync(Path.Join(testConfigDisabledRoot, "testconfig.json"), "{}\n").ConfigureAwait(false);
        var testConfigDisabledEvaluation = JsonDocument.Parse(await _run(
            "dotnet",
            $"msbuild \"{Path.Join(testConfigDisabledRoot, "Consumer.Tests.csproj")}\" -getItem:None",
            _createSdkEnvironment(testConfigDisabledRoot)));
        var testConfigDisabledItem = _findItem(testConfigDisabledEvaluation, "None", "testconfig.json");
        Assert.IsFalse(testConfigDisabledItem is not null &&
            string.Equals(testConfigDisabledItem.GetValueOrDefault("DefiningProjectName"), "Sdk", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures exact analyzer references, opt-outs, SQL exclusion, and package boundaries compose with Build.
    /// </summary>
    [TestMethod]
    public async Task SdkAnalyzerReferencesAreExactSwitchableAndCapabilitySafe()
    {
        const string packageVersion = "999.9.15";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-references");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        using var baseline = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "baseline",
            "Consumer.csproj",
            _createSdkCSharpProject());
        var packageReferences = _getPackageReferences(baseline);
        Assert.AreEqual(packageVersion, packageReferences["Ark.Tools.Build"]["Version"]);
        Assert.AreEqual("true", packageReferences["Ark.Tools.Build"]["IsImplicitlyDefined"]);
        foreach (var analyzer in _sdkAnalyzerVersions)
        {
            Assert.AreEqual(analyzer.Value, packageReferences[analyzer.Key]["Version"], analyzer.Key);
            Assert.AreEqual("true", packageReferences[analyzer.Key]["IsImplicitlyDefined"], analyzer.Key);
            Assert.AreEqual("all", packageReferences[analyzer.Key]["PrivateAssets"], analyzer.Key);
            Assert.AreEqual(
                "runtime;build;native;contentfiles;analyzers;buildtransitive",
                packageReferences[analyzer.Key]["IncludeAssets"],
                analyzer.Key);
        }
        foreach (var excludedPackage in _excludedSdkPackages)
        {
            Assert.IsFalse(packageReferences.ContainsKey(excludedPackage), excludedPackage);
        }
        Assert.IsFalse(packageReferences.Keys.Any(package =>
            package.StartsWith("Reqnroll.", StringComparison.Ordinal) ||
            package.StartsWith("Microsoft.Testing.Extensions.", StringComparison.Ordinal)));

        var switches = new Dictionary<string, (string Package, string Item, string Asset)>
        {
            ["EnableArkToolsNetAnalyzers"] = ("Microsoft.CodeAnalysis.NetAnalyzers", "GlobalAnalyzerConfigFiles", "Ark.Tools.NetAnalyzers.globalconfig"),
            ["EnableArkToolsBannedApi"] = ("Microsoft.CodeAnalysis.BannedApiAnalyzers", "AdditionalFiles", "BannedSymbols.Ark.Tools.txt"),
            ["EnableArkToolsMeziantouAnalyzer"] = ("Meziantou.Analyzer", "GlobalAnalyzerConfigFiles", "Ark.Tools.MeziantouAnalyzer.globalconfig"),
            ["EnableArkToolsVisualStudioThreading"] = ("Microsoft.VisualStudio.Threading.Analyzers", "GlobalAnalyzerConfigFiles", "Ark.Tools.VisualStudioThreading.globalconfig"),
            ["EnableArkToolsErrorProne"] = ("ErrorProne.NET.CoreAnalyzers", "GlobalAnalyzerConfigFiles", "Ark.Tools.ErrorProne.globalconfig")
        };
        foreach (var feature in switches)
        {
            using var disabled = await _evaluateSdkAsync(
                fixtureRoot,
                feed,
                $"disabled-{feature.Key}",
                "Consumer.csproj",
                _createSdkCSharpProject(),
                directoryProperties: $"<{feature.Key}>false</{feature.Key}>");
            Assert.IsFalse(_getPackageReferences(disabled).ContainsKey(feature.Value.Package), feature.Key);
            CollectionAssert.DoesNotContain(
                _getItemIdentities(disabled, feature.Value.Item)
                    .Select(identity => Path.GetFileName(identity) ?? "")
                    .ToArray(),
                feature.Value.Asset,
                feature.Key);
        }

        using var sql = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "sql",
            "Consumer.sqlproj",
            _createSdkSqlProject());
        _assertProperties(sql, new Dictionary<string, string>
        {
            ["UsingMicrosoftBuildSqlSdk"] = "true",
            ["AnalysisLevel"] = "",
            ["LangVersion"] = ""
        });
        var sqlPackages = _getPackageReferences(sql);
        Assert.IsTrue(sqlPackages.ContainsKey("Ark.Tools.Build"));
        foreach (var analyzer in _sdkAnalyzerVersions.Keys)
        {
            Assert.IsFalse(sqlPackages.ContainsKey(analyzer), analyzer);
        }
        Assert.AreEqual(0, _getAllArkBuildConfigurationFileNames(sql).Length);
        Assert.IsTrue(File.Exists(Path.Join(fixtureRoot, "sql", "packages.lock.json")));

        using var fsharp = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "fsharp",
            "Consumer.fsproj",
            _createSdkFSharpProject());
        _assertProperties(fsharp, new Dictionary<string, string>
        {
            ["AnalysisLevel"] = "",
            ["LangVersion"] = ""
        });
        var fsharpPackages = _getPackageReferences(fsharp);
        Assert.IsTrue(fsharpPackages.ContainsKey("Ark.Tools.Build"));
        foreach (var analyzer in _sdkAnalyzerVersions.Keys)
        {
            Assert.IsFalse(fsharpPackages.ContainsKey(analyzer), analyzer);
        }
        Assert.IsTrue(File.Exists(Path.Join(fixtureRoot, "fsharp", "packages.lock.json")));
    }

    /// <summary>
    /// Ensures generated lock files, locked CI restore, and CPM ownership boundaries are enforced.
    /// </summary>
    [TestMethod]
    public async Task SdkLockFileAndCpmBoundariesAreEnforced()
    {
        const string packageVersion = "999.9.16";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-lock-and-cpm");
        var feed = await _createSdkFeedAsync(root, fixtureRoot, packageVersion);

        var lockedRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "locked",
            "Consumer.csproj",
            _createSdkCSharpProject());
        var lockedEnvironment = _createSdkEnvironment(fixtureRoot);
        await _run(
            "dotnet",
            $"restore \"{Path.Join(lockedRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);
        var lockFile = Path.Join(lockedRoot, "packages.lock.json");
        Assert.IsTrue(File.Exists(lockFile));
        using (var lockJson = JsonDocument.Parse(await File.ReadAllTextAsync(lockFile).ConfigureAwait(false)))
        {
            var dependencies = lockJson.RootElement.GetProperty("dependencies").GetProperty("net10.0");
            foreach (var analyzer in _sdkAnalyzerVersions)
            {
                Assert.AreEqual(analyzer.Value, dependencies.GetProperty(analyzer.Key).GetProperty("resolved").GetString(), analyzer.Key);
            }
        }
        lockedEnvironment["CI"] = "true";
        Directory.Delete(Path.Join(lockedRoot, "obj"), true);
        await _run(
            "dotnet",
            $"restore \"{Path.Join(lockedRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);

        await File.WriteAllTextAsync(
            Path.Join(lockedRoot, "Consumer.csproj"),
            _createSdkCSharpProject(targetFramework: "net8.0")).ConfigureAwait(false);
        Directory.Delete(Path.Join(lockedRoot, "obj"), true);
        var lockedFailure = await _runForExitCode(
            "dotnet",
            $"restore \"{Path.Join(lockedRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);
        Assert.AreNotEqual(0, lockedFailure.ExitCode);
        StringAssert.Contains(lockedFailure.Output, "NU1004", StringComparison.Ordinal);

        await File.WriteAllTextAsync(
            Path.Join(lockedRoot, "Consumer.csproj"),
            _createSdkCSharpProject("<RestoreLockedMode>false</RestoreLockedMode>", targetFramework: "net8.0")).ConfigureAwait(false);
        await _run(
            "dotnet",
            $"restore \"{Path.Join(lockedRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);

        var cpmRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "cpm",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            """
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
</Project>
""");
        await _run(
            "dotnet",
            $"restore \"{Path.Join(cpmRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(cpmRoot, "NuGet.Config")}\"",
            _createSdkEnvironment(fixtureRoot));
        await File.WriteAllTextAsync(
            Path.Join(cpmRoot, "Directory.Packages.props"),
            """
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup><PackageVersion Include="Meziantou.Analyzer" Version="3.0.160" /></ItemGroup>
</Project>
""").ConfigureAwait(false);
        Directory.Delete(Path.Join(cpmRoot, "obj"), true);
        var cpmFailure = await _runForExitCode(
            "dotnet",
            $"restore \"{Path.Join(cpmRoot, "Consumer.csproj")}\" --configfile \"{Path.Join(cpmRoot, "NuGet.Config")}\"",
            _createSdkEnvironment(fixtureRoot));
        Assert.AreNotEqual(0, cpmFailure.ExitCode);
        StringAssert.Contains(cpmFailure.Output, "NU1009", StringComparison.Ordinal);
        StringAssert.Contains(cpmFailure.Output, "Meziantou.Analyzer", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures SponsorLink removal is exact and independently switchable.
    /// </summary>
    [TestMethod]
    public async Task SponsorLinkRemovalIsExactAndSwitchable()
    {
        const string packageVersion = "999.9.13";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-sponsor-link");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = Path.Join(fixtureRoot, "feed");
        Directory.CreateDirectory(feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");

        var analyzerItems = """
<Analyzer Include="DevLooped.SponsorLink.dll" />
<Analyzer Include="Moq.CodeAnalysis.dll" />
<Analyzer Include="Preserved.Analyzer.dll" />
""";
        var project = _createCSharpProject(packageVersion, "", "", analyzerItems);
        var enabled = await _evaluateTargetItemsAsync(fixtureRoot, feed, "sponsor-enabled", project, "");
        CollectionAssert.AreEqual(_preservedAnalyzer, enabled);
        var disabled = await _evaluateTargetItemsAsync(
            fixtureRoot,
            feed,
            "sponsor-disabled",
            project,
            "<EnableArkToolsSponsorLinkRemoval>false</EnableArkToolsSponsorLinkRemoval>");
        CollectionAssert.AreEquivalent(
            _allSyntheticAnalyzers,
            disabled);
    }

    /// <summary>
    /// Ensures packed Build assets select only the accepted capability-specific policy in clean consumers.
    /// </summary>
    [TestMethod]
    public async Task BuildBaselineIsCapabilitySafeAndOverridable()
    {
        const string packageVersion = "999.9.10";
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-build-baseline");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = Path.Join(fixtureRoot, "feed");
        Directory.CreateDirectory(feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");

        var csharpProject = _createCSharpProject(packageVersion);
        using var csharp = await _evaluateAsync(fixtureRoot, feed, "csharp", "Consumer.csproj", csharpProject);
        _assertProperties(csharp, new Dictionary<string, string>
        {
            ["TreatWarningsAsErrors"] = "true",
            ["MSBuildTreatWarningsAsErrors"] = "true",
            ["Nullable"] = "enable",
            ["ImplicitUsings"] = "enable",
            ["GenerateDocumentationFile"] = "true",
            ["Features"] = "strict",
            ["ReportAnalyzer"] = "true",
            ["EnforceCodeStyleInBuild"] = "true",
            ["TreatTSqlWarningsAsErrors"] = "",
            ["RunSqlCodeAnalysis"] = ""
        });
        _assertProperties(csharp, new Dictionary<string, string>
        {
            ["DebugType"] = "portable",
            ["DebugSymbols"] = "true",
            ["Deterministic"] = "true",
            ["EmbedUntrackedSources"] = "true",
            ["EnableNETAnalyzers"] = "true"
        });
        CollectionAssert.AreEquivalent(
            _standardImplicitUsings,
            _getItemIdentities(csharp, "Using"));
        CollectionAssert.AreEqual(_buildPackageReference, _getItemIdentities(csharp, "PackageReference"));

        using var control = await _evaluateAsync(
            fixtureRoot,
            feed,
            "control",
            "Consumer.csproj",
            _createCSharpProject(null));
        _assertPropertiesMatch(csharp, control, _boundaryProperties);
        _assertItemsMatch(csharp, control, _boundaryItems, "Ark.Tools.Build");

        var overrides = string.Join(
            Environment.NewLine,
            "<TreatWarningsAsErrors>false</TreatWarningsAsErrors>",
            "<MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors>",
            "<Nullable>disable</Nullable>",
            "<ImplicitUsings>disable</ImplicitUsings>",
            "<GenerateDocumentationFile>false</GenerateDocumentationFile>",
            "<Features>none</Features>",
            "<ReportAnalyzer>false</ReportAnalyzer>",
            "<EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>",
            "<TreatTSqlWarningsAsErrors>false</TreatTSqlWarningsAsErrors>",
            "<RunSqlCodeAnalysis>false</RunSqlCodeAnalysis>");
        using var overridden = await _evaluateAsync(
            fixtureRoot,
            feed,
            "overridden",
            "Consumer.csproj",
            _createCSharpProject(packageVersion, overrides));
        _assertProperties(overridden, new Dictionary<string, string>
        {
            ["TreatWarningsAsErrors"] = "false",
            ["MSBuildTreatWarningsAsErrors"] = "false",
            ["Nullable"] = "disable",
            ["ImplicitUsings"] = "disable",
            ["GenerateDocumentationFile"] = "false",
            ["Features"] = "none",
            ["ReportAnalyzer"] = "false",
            ["EnforceCodeStyleInBuild"] = "false",
            ["TreatTSqlWarningsAsErrors"] = "false",
            ["RunSqlCodeAnalysis"] = "false"
        });

        using var directoryDisabled = await _evaluateAsync(
            fixtureRoot,
            feed,
            "directory-disabled",
            "Consumer.csproj",
            csharpProject,
            "<EnableArkToolsBuild>false</EnableArkToolsBuild>");
        _assertPropertiesMatch(directoryDisabled, control, _selectedProperties);
        Assert.AreEqual("", _getProperty(directoryDisabled, "ArkToolsBuildImported"));

        using var globallyDisabled = await _evaluateAsync(
            fixtureRoot,
            feed,
            "globally-disabled",
            "Consumer.csproj",
            csharpProject,
            globalDisable: true);
        _assertPropertiesMatch(globallyDisabled, control, _selectedProperties);
        Assert.AreEqual("", _getProperty(globallyDisabled, "ArkToolsBuildImported"));

        var sqlProject = _createSqlProject(packageVersion);
        using var sql = await _evaluateAsync(fixtureRoot, feed, "sql", "Consumer.sqlproj", sqlProject);
        using var sqlControl = await _evaluateAsync(
            fixtureRoot,
            feed,
            "sql-control",
            "Consumer.sqlproj",
            _createSqlProject(null));
        _assertProperties(sql, new Dictionary<string, string>
        {
            ["UsingMicrosoftBuildSqlSdk"] = "true",
            ["TreatWarningsAsErrors"] = "true",
            ["MSBuildTreatWarningsAsErrors"] = "true",
            ["TreatTSqlWarningsAsErrors"] = "true",
            ["RunSqlCodeAnalysis"] = "true"
        });
        _assertPropertiesMatch(
            sql,
            sqlControl,
            ["Nullable", "ImplicitUsings", "GenerateDocumentationFile", "Features", "ReportAnalyzer", "EnforceCodeStyleInBuild"]);
        _assertPropertiesMatch(sql, sqlControl, _boundaryProperties);
        _assertItemsMatch(sql, sqlControl, _boundaryItems, "Ark.Tools.Build");

        var sqlOverrides = string.Join(
            Environment.NewLine,
            "<TreatWarningsAsErrors>false</TreatWarningsAsErrors>",
            "<MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors>",
            "<TreatTSqlWarningsAsErrors>false</TreatTSqlWarningsAsErrors>",
            "<RunSqlCodeAnalysis>false</RunSqlCodeAnalysis>");
        using var sqlOverridden = await _evaluateAsync(
            fixtureRoot,
            feed,
            "sql-overridden",
            "Consumer.sqlproj",
            _createSqlProject(packageVersion, sqlOverrides));
        _assertProperties(sqlOverridden, new Dictionary<string, string>
        {
            ["TreatWarningsAsErrors"] = "false",
            ["MSBuildTreatWarningsAsErrors"] = "false",
            ["TreatTSqlWarningsAsErrors"] = "false",
            ["RunSqlCodeAnalysis"] = "false"
        });

        using var fsharp = await _evaluateAsync(
            fixtureRoot,
            feed,
            "fsharp",
            "Consumer.fsproj",
            _createFSharpProject(packageVersion));
        using var fsharpControl = await _evaluateAsync(
            fixtureRoot,
            feed,
            "fsharp-control",
            "Consumer.fsproj",
            _createFSharpProject(null));
        _assertProperties(fsharp, new Dictionary<string, string>
        {
            ["TreatWarningsAsErrors"] = "true",
            ["MSBuildTreatWarningsAsErrors"] = "true"
        });
        _assertPropertiesMatch(
            fsharp,
            fsharpControl,
            ["Nullable", "ImplicitUsings", "GenerateDocumentationFile", "Features", "ReportAnalyzer", "EnforceCodeStyleInBuild", "TreatTSqlWarningsAsErrors", "RunSqlCodeAnalysis"]);
        _assertPropertiesMatch(fsharp, fsharpControl, _boundaryProperties);
        _assertItemsMatch(fsharp, fsharpControl, _boundaryItems, "Ark.Tools.Build");
    }

    private static string _createSdkCSharpProject(
        string properties = "",
        string targetFramework = "net10.0")
    {
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{targetFramework}</TargetFramework>
    {properties}
  </PropertyGroup>
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
""";
    }

    private static string _createSdkSqlProject()
    {
        return """
<Project DefaultTargets="Build">
  <Sdk Name="Microsoft.Build.Sql" Version="2.2.0" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <DSP>Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider</DSP>
  </PropertyGroup>
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
""";
    }

    private static string _createSdkFSharpProject()
    {
        return """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
""";
    }

    private static async Task<string> _createSdkFeedAsync(
        string root,
        string fixtureRoot,
        string packageVersion)
    {
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = Path.Join(fixtureRoot, "feed");
        Directory.CreateDirectory(feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");
        await _run(
            "dotnet",
            $"pack \"{Path.Join(root, "src", "sdk", "Ark.Tools.Sdk", "Ark.Tools.Sdk.csproj")}\" -c Debug -o \"{feed}\" -p:PackageVersion={packageVersion}");
        return feed;
    }

    private static async Task<string> _createSdkScenarioAsync(
        string fixtureRoot,
        string feed,
        string scenario,
        string projectFileName,
        string project,
        string directoryPackages = "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>",
        string directoryProperties = "")
    {
        var scenarioRoot = Path.Join(fixtureRoot, scenario);
        Directory.CreateDirectory(scenarioRoot);
        var packageVersion = Directory.GetFiles(feed, "Ark.Tools.Sdk.*.nupkg")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name?["Ark.Tools.Sdk.".Length..] ?? "")
            .Single();
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "Directory.Build.props"),
            $"<Project><PropertyGroup>{directoryProperties}</PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(scenarioRoot, "Directory.Build.targets"), "<Project />").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(scenarioRoot, "Directory.Packages.props"), directoryPackages).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "global.json"),
            $"{{\"sdk\":{{\"version\":\"10.0.400\",\"rollForward\":\"latestFeature\"}},\"msbuild-sdks\":{{\"Ark.Tools.Sdk\":\"{packageVersion}\"}}}}").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "NuGet.Config"),
            $"<configuration><packageSources><clear /><add key=\"local\" value=\"{feed}\" /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources></configuration>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(scenarioRoot, projectFileName), project).ConfigureAwait(false);
        return scenarioRoot;
    }

    private static async Task<JsonDocument> _evaluateSdkAsync(
        string fixtureRoot,
        string feed,
        string scenario,
        string projectFileName,
        string project,
        string? directoryPackages = null,
        IDictionary<string, string>? environment = null,
        string directoryProperties = "")
    {
        var scenarioRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            scenario,
            projectFileName,
            project,
            directoryPackages ?? "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>",
            directoryProperties);
        var processEnvironment = environment ?? _createSdkEnvironment(fixtureRoot);
        var projectPath = Path.Join(scenarioRoot, projectFileName);
        await _run(
            "dotnet",
            $"restore \"{projectPath}\" --configfile \"{Path.Join(scenarioRoot, "NuGet.Config")}\" -p:RestoreLockedMode=false",
            processEnvironment);
        var properties = new[]
        {
            "_IsGitHubActions",
            "ContinuousIntegrationBuild",
            "RestorePackagesWithLockFile",
            "RestoreSerializeGlobalProperties",
            "RestoreLockedMode",
            "NuGetAudit",
            "NuGetAuditMode",
            "NuGetAuditLevel",
            "AnalysisLevel",
            "LangVersion",
            "IsTestProject",
            "IsPackable",
            "WarnOnPackingNonPackableProject",
            "OutputType",
            "ExcludeByAttribute",
            "MinimumExpectedTests",
            "ReqnrollUseIntermediateOutputPathForCodeBehind",
            "ReqnrollDeleteObsoleteCodeBehindFilesOnClean",
            "TestingPlatformCommandLineArguments",
            "WarningsNotAsErrors",
            "UsingMicrosoftBuildSqlSdk",
            "GenerateSBOM",
            "PolyUseEmbeddedAttribute",
            "AccelerateBuildsInVisualStudio",
            "EnablePackageValidation",
            "IncludeSymbols",
            "SymbolPackageFormat",
            "EnableSourceControlManagerQueries",
            "EnableSourceLink"
        };
        var output = await _run(
            "dotnet",
            $"msbuild \"{projectPath}\" -getProperty:{string.Join(",", properties)} -getItem:PackageReference,Using,EditorConfigFiles,GlobalAnalyzerConfigFiles,AdditionalFiles",
            processEnvironment);
        return JsonDocument.Parse(output);
    }

    private static Dictionary<string, string> _createSdkEnvironment(string fixtureRoot)
    {
        return new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Join(fixtureRoot, "packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(fixtureRoot, "http-cache"),
            ["TF_BUILD"] = "",
            ["GITHUB_ACTIONS"] = "",
            ["CI"] = ""
        };
    }

    private static Dictionary<string, Dictionary<string, string>> _getPackageReferences(JsonDocument evaluation)
    {
        return evaluation.RootElement.GetProperty("Items").GetProperty("PackageReference")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("Identity").GetString() ?? "",
                item => item.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => property.Value.GetString() ?? ""),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string _createCSharpProject(
        string? packageVersion,
        string properties = "",
        string packageReferences = "",
        string items = "")
    {
        var additionalItems = string.IsNullOrWhiteSpace(packageReferences) && string.IsNullOrWhiteSpace(items)
            ? ""
            : $"<ItemGroup>{packageReferences}{items}</ItemGroup>";
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    {properties}
  </PropertyGroup>
  {_createPackageReference(packageVersion)}
  {additionalItems}
</Project>
""";
    }

    private static string _createFSharpProject(string? packageVersion)
    {
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  {_createPackageReference(packageVersion)}
</Project>
""";
    }

    private static string _createSqlProject(string? packageVersion, string properties = "")
    {
        return $"""
<Project DefaultTargets="Build">
  <Sdk Name="Microsoft.Build.Sql" Version="2.2.0" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <DSP>Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider</DSP>
    {properties}
  </PropertyGroup>
  {_createPackageReference(packageVersion)}
</Project>
""";
    }

    private static string _createPackageReference(string? packageVersion)
    {
        return packageVersion is null
            ? ""
            : $"<ItemGroup><PackageReference Include=\"Ark.Tools.Build\" Version=\"{packageVersion}\" /></ItemGroup>";
    }

    private static async Task<JsonDocument> _evaluateAsync(
        string fixtureRoot,
        string feed,
        string scenario,
        string projectFileName,
        string project,
        string directoryProperties = "",
        bool globalDisable = false)
    {
        var scenarioRoot = Path.Join(fixtureRoot, scenario);
        Directory.CreateDirectory(scenarioRoot);
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "Directory.Build.props"),
            $"<Project><PropertyGroup><ArkToolsSdkProject>true</ArkToolsSdkProject><RestorePackagesWithLockFile>false</RestorePackagesWithLockFile><EnablePackageValidation>false</EnablePackageValidation>{directoryProperties}</PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(scenarioRoot, "Directory.Build.targets"), "<Project />").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "Directory.Packages.props"),
            "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "NuGet.Config"),
            $"<configuration><packageSources><clear /><add key=\"local\" value=\"{feed}\" /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources></configuration>").ConfigureAwait(false);
        var projectPath = Path.Join(scenarioRoot, projectFileName);
        await File.WriteAllTextAsync(projectPath, project).ConfigureAwait(false);
        var environment = _createEnvironment(scenarioRoot);
        var globalProperty = globalDisable ? " -p:EnableArkToolsBuild=false" : "";
        await _run(
            "dotnet",
            $"restore \"{projectPath}\" --configfile \"{Path.Join(scenarioRoot, "NuGet.Config")}\"{globalProperty}",
            environment);
        var propertyNames = _selectedProperties.Concat(_boundaryProperties).Append("ArkToolsBuildImported").Append("UsingMicrosoftBuildSqlSdk");
        var output = await _run(
            "dotnet",
            $"msbuild \"{projectPath}\" -getProperty:{string.Join(",", propertyNames)} -getItem:{string.Join(",", _boundaryItems.Append("Using").Append("EditorConfigFiles").Append("GlobalAnalyzerConfigFiles"))}{globalProperty}",
            environment);
        return JsonDocument.Parse(output);
    }

    private static async Task<string> _createCompilerScenarioAsync(
        string fixtureRoot,
        string feed,
        string scenario,
        string packageVersion,
        string source)
    {
        using var evaluation = await _evaluateAsync(
            fixtureRoot,
            feed,
            scenario,
            "Consumer.csproj",
            _createCSharpProject(packageVersion));
        var scenarioRoot = Path.Join(fixtureRoot, scenario);
        if (!string.IsNullOrEmpty(source))
        {
            await File.WriteAllTextAsync(Path.Join(scenarioRoot, "Consumer.cs"), source).ConfigureAwait(false);
        }
        return scenarioRoot;
    }

    private static async Task<string[]> _evaluateTargetItemsAsync(
        string fixtureRoot,
        string feed,
        string scenario,
        string project,
        string directoryProperties)
    {
        using var evaluation = await _evaluateAsync(
            fixtureRoot,
            feed,
            scenario,
            "Consumer.csproj",
            project,
            directoryProperties);
        var scenarioRoot = Path.Join(fixtureRoot, scenario);
        var output = await _run(
            "dotnet",
            $"msbuild \"{Path.Join(scenarioRoot, "Consumer.csproj")}\" -target:Disable_SponsorLink -getItem:Analyzer",
            _createEnvironment(scenarioRoot));
        using var targetEvaluation = JsonDocument.Parse(output);
        return _getItemIdentities(targetEvaluation, "Analyzer")
            .Select(identity => Path.GetFileName(identity) ?? "")
            .Where(_allSyntheticAnalyzers.Contains)
            .ToArray();
    }

    private static Dictionary<string, string> _createEnvironment(string scenarioRoot)
    {
        return new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Join(scenarioRoot, "packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(scenarioRoot, "http-cache")
        };
    }

    private static int _countConfiguredDiagnostics(string path)
    {
        return File.ReadLines(path).Count(line =>
            line.TrimStart().StartsWith("dotnet_diagnostic.", StringComparison.Ordinal) &&
            line.Contains(".severity", StringComparison.Ordinal));
    }

    private static void _assertDiagnosticOwner(IEnumerable<string> paths, string diagnosticId, string expectedPath)
    {
        var owners = paths
            .Where(path => File.ReadLines(path).Any(line =>
                line.Contains($"dotnet_diagnostic.{diagnosticId}.severity", StringComparison.Ordinal)))
            .ToArray();
        Assert.HasCount(1, owners, diagnosticId);
        Assert.AreEqual(expectedPath, owners[0], diagnosticId);
    }

    private static string[] _getAllArkBuildConfigurationFileNames(JsonDocument evaluation)
    {
        return _getArkBuildItemFileNames(evaluation, "EditorConfigFiles")
            .Concat(_getArkBuildItemFileNames(evaluation, "GlobalAnalyzerConfigFiles"))
            .Concat(_getArkBuildItemFileNames(evaluation, "AdditionalFiles"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] _getArkBuildItemFileNames(JsonDocument evaluation, string itemName)
    {
        return _getItemIdentities(evaluation, itemName)
            .Where(identity => identity.Contains("ark.tools.build", StringComparison.OrdinalIgnoreCase))
            .Select(identity => Path.GetFileName(identity) ?? "")
            .ToArray();
    }

    private static void _assertProperties(JsonDocument evaluation, IReadOnlyDictionary<string, string> expected)
    {
        foreach (var pair in expected)
        {
            Assert.AreEqual(pair.Value, _getProperty(evaluation, pair.Key), pair.Key);
        }
    }

    private static void _assertPropertiesMatch(JsonDocument actual, JsonDocument expected, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.AreEqual(_getProperty(expected, propertyName), _getProperty(actual, propertyName), propertyName);
        }
    }

    private static void _assertItemsMatch(
        JsonDocument actual,
        JsonDocument expected,
        IEnumerable<string> itemNames,
        string ignoredIdentity)
    {
        foreach (var itemName in itemNames)
        {
            var actualItems = _getItemIdentities(actual, itemName)
                .Where(identity => !identity.Contains(ignoredIdentity, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            CollectionAssert.AreEquivalent(_getItemIdentities(expected, itemName), actualItems, itemName);
        }
    }

    private static string _getProperty(JsonDocument evaluation, string propertyName)
    {
        return evaluation.RootElement.GetProperty("Properties").GetProperty(propertyName).GetString() ?? "";
    }

    private static string[] _getItemIdentities(JsonDocument evaluation, string itemName)
    {
        return evaluation.RootElement.GetProperty("Items").GetProperty(itemName)
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString() ?? "")
            .ToArray();
    }

    private static string[] _getSdkItemIdentities(JsonDocument evaluation, string itemName)
    {
        return evaluation.RootElement.GetProperty("Items").GetProperty(itemName)
            .EnumerateArray()
            .Where(item => item.TryGetProperty("DefiningProjectName", out var definingProjectName) &&
                string.Equals(definingProjectName.GetString(), "Sdk", StringComparison.Ordinal))
            .Select(item => item.GetProperty("Identity").GetString() ?? "")
            .ToArray();
    }

    private static Dictionary<string, string>? _findItem(JsonDocument evaluation, string itemName, string fileName)
    {
        var matches = evaluation.RootElement.GetProperty("Items").GetProperty(itemName)
            .EnumerateArray()
            .Select(item =>
            {
                var identity = item.GetProperty("Identity").GetString() ?? "";
                var metadata = item.EnumerateObject()
                    .Where(property => !string.Equals(property.Name, "Identity", StringComparison.Ordinal))
                    .ToDictionary(
                        property => property.Name,
                        property => property.Value.GetString() ?? "",
                        StringComparer.Ordinal);
                return new
                {
                    Identity = identity,
                    DefiningProjectName = metadata.TryGetValue("DefiningProjectName", out var definingProjectName) ? definingProjectName : "",
                    Metadata = metadata
                };
            })
            .Where(item => string.Equals(Path.GetFileName(item.Identity), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => string.Equals(item.DefiningProjectName, "Sdk", StringComparison.Ordinal))
            .ThenByDescending(item => item.Metadata.ContainsKey("CopyToOutputDirectory"))
            .ThenByDescending(item => item.Metadata.ContainsKey("CopyToPublishDirectory"))
            .ToArray();

        return matches.Length == 0 ? null : matches[0].Metadata;
    }

    private static async Task<string> _run(string fileName, string arguments, IDictionary<string, string>? environment = null)
    {
        var result = await _runForExitCode(fileName, arguments, environment);
        Assert.AreEqual(0, result.ExitCode, result.Output);
        return result.Output;
    }

    private static async Task<(int ExitCode, string Output)> _runForExitCode(
        string fileName,
        string arguments,
        IDictionary<string, string>? environment = null)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        return (process.ExitCode, $"{output}{Environment.NewLine}{error}");
    }
}
