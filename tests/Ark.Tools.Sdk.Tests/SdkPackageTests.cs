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
        await File.WriteAllTextAsync(Path.Join(consumer, "global.json"), """{"sdk":{"version":"10.0.400","rollForward":"latestFeature"},"msbuild-sdks":{"Ark.Tools.Sdk":"999.9.9"}}""").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Join(consumer, "NuGet.Config"), $"<configuration><packageSources><clear /><add key=\"local\" value=\"{feed}\" /></packageSources></configuration>").ConfigureAwait(false);
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
        CollectionAssert.AreEqual(
            _canonicalProperties,
            props.Descendants("PropertyGroup").Elements().Select(element => element.Name.LocalName).ToArray());
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
