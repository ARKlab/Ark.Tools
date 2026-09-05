// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Ark.Tools.Sdk.Tests;

/// <summary>
/// Verifies the SDK and build packages through clean consumer projects.
/// </summary>
[TestClass]
public sealed class SdkPackageTests
{
    private const string _packageVersion = "999.9.9";
    private static string _root = "";
    private static string _feed = "";
    private static string _packageCache = "";
    private static string _httpCache = "";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _ = context;
        _root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var artifactsRoot = Path.Join(_root, "artifacts", "sdk-test-shared");
        _feed = Path.Join(artifactsRoot, "feed");
        _packageCache = Path.Join(artifactsRoot, "packages");
        _httpCache = Path.Join(artifactsRoot, "http-cache");
        if (Directory.Exists(artifactsRoot))
        {
            Directory.Delete(artifactsRoot, true);
        }
        Directory.CreateDirectory(_feed);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(_root, "src", "sdk", "Ark.Tools.Build", "Ark.Tools.Build.csproj")}\" -c Debug -o \"{_feed}\" -p:PackageVersion={_packageVersion}")
            .ConfigureAwait(false);
        await _run(
            "dotnet",
            $"pack \"{Path.Join(_root, "src", "sdk", "Ark.Tools.Sdk", "Ark.Tools.Sdk.csproj")}\" -c Debug -o \"{_feed}\" -p:PackageVersion={_packageVersion}")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the SDK package carries repository provenance without provider-specific Source Link packages.
    /// </summary>
    [TestMethod]
    public async Task SdkPackageContainsRepositoryMetadata()
    {
        var packagePath = Directory.GetFiles(_feed, "Ark.Tools.Sdk.*.nupkg").Single();
        using var archive = await ZipFile.OpenReadAsync(packagePath).ConfigureAwait(false);
        var nuspecEntry = archive.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        await using var nuspecStream = await nuspecEntry.OpenAsync().ConfigureAwait(false);
        var nuspec = await XDocument.LoadAsync(nuspecStream, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);
        var repository = nuspec.Descendants().Single(static element => element.Name.LocalName == "repository");

        Assert.AreEqual("https://github.com/ARKlab/Ark.Tools", repository.Attribute("url")?.Value);
        Assert.AreEqual("git", repository.Attribute("type")?.Value);
        var gitCommit = (await _run("git", "rev-parse HEAD")).Trim();
        Assert.AreEqual(gitCommit, repository.Attribute("commit")?.Value);
    }

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

    private static readonly string[] _composedBannedApiAssets =
    [
        "BannedSymbols.Ark.txt",
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
            ["Meziantou.Analyzer"] = "3.0.205",
            ["ErrorProne.NET.CoreAnalyzers"] = "0.1.2"
        };

    private const string _visualStudioThreadingAnalyzerVersion = "18.7.23";

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

#if false
    /// <summary>
    /// Ensures every packaged configuration asset is independently switchable and capability safe.
    /// </summary>
    [TestMethod]
    public async Task AnalyzerConfigurationAssetsAreSwitchableAndCapabilitySafe()
    {
        const string packageVersion = _packageVersion;
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-configuration");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = _feed;

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
            ["EnableArkToolsBannedApi"] = "BannedSymbols.Ark.txt"
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
        using var local = await _evaluateAsync(
            fixtureRoot,
            feed,
            "local-config-not-discovered",
            "Consumer.csproj",
            _createCSharpProject(packageVersion),
            $"<DirectoryBuildPropsPath>{Path.Join(localConfigRoot, "Directory.Build.props")}</DirectoryBuildPropsPath>");
        CollectionAssert.DoesNotContain(_getItemIdentities(local, "GlobalAnalyzerConfigFiles"), localConfig);

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
#endif

    /// <summary>
    /// Ensures compiler configuration precedence and packaged banned symbols work in consumer source.
    /// </summary>
    [TestMethod]
    public async Task CompilerConfigurationPrecedenceAndBannedApiAreEnforced()
    {
        const string packageVersion = _packageVersion;
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-compiler");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = _feed;

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
            "",
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
            _getItemIdentities(banned, "AdditionalFiles").Select(static identity => Path.GetFileName(identity) ?? "").ToArray());
        var bannedError = await _runForExitCode(
            "dotnet",
            $"build \"{Path.Join(bannedRoot, "Consumer.csproj")}\" --no-restore",
            _createEnvironment(bannedRoot));
        Assert.AreEqual(0, bannedError.ExitCode);
        Assert.IsFalse(bannedError.Output.Contains("RS0030", StringComparison.Ordinal));
    }

#if false
    /// <summary>
    /// Ensures SDK restore, audit, compiler, CI, and test-classification policy is early and overrideable.
    /// </summary>
    [TestMethod]
    public async Task SdkPackagingProfileAddsPackageBackedToolingAndOptOuts()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-packaging-profile");
        var feed = _prepareSdkFixture(fixtureRoot);

        using var baseline = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "baseline",
            "Consumer.csproj",
            _createSdkCSharpProject());
        var packageReferences = _getPackageReferences(baseline);
        Assert.AreEqual("11.2.0", packageReferences["Polyfill"]["Version"]);
        Assert.AreEqual("4.1.5", packageReferences["Microsoft.Sbom.Targets"]["Version"]);
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
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-restore-policy");
        var feed = _prepareSdkFixture(fixtureRoot);

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

    }
#endif

    /// <summary>
    /// Ensures the SDK adds only the accepted MTP test extensions and default safety settings for test projects.
    /// </summary>
    [TestMethod]
    public async Task SdkTestProfileAddsFrameworkNeutralMtpExtensionsAndDefaults()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-mtp-profile");
        var feed = _prepareSdkFixture(fixtureRoot);

        using var baseline = await _evaluateSdkAsync(
            fixtureRoot,
            feed,
            "baseline",
            "Consumer.Tests.csproj",
            _createSdkCSharpProject());
        var packageReferences = _getPackageReferences(baseline);
        foreach (var package in new[]
        {
            ("Microsoft.Testing.Extensions.CrashDump", "2.4.0"),
            ("Microsoft.Testing.Extensions.CodeCoverage", "18.11.0"),
            ("Microsoft.Testing.Extensions.HangDump", "2.4.0"),
            ("Microsoft.Testing.Extensions.HotReload", "2.4.0"),
            ("Microsoft.Testing.Extensions.Retry", "2.4.0"),
            ("Microsoft.Testing.Extensions.TrxReport", "2.4.0"),
            ("Microsoft.Testing.Extensions.AzureDevOpsReport", "2.4.0")
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
        var baselineArguments = _getProperty(baseline, "TestingPlatformCommandLineArguments");
        Assert.IsTrue(baselineArguments.Contains("--report-trx", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--crashdump", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--crashdump-type mini", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--hangdump", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--hangdump-type mini", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--hangdump-timeout 10m", StringComparison.Ordinal));
        Assert.IsTrue(baselineArguments.Contains("--minimum-expected-tests 1", StringComparison.Ordinal));
        Assert.IsFalse(packageReferences.ContainsKey("MSTest.TestFramework"));
        Assert.IsFalse(packageReferences.ContainsKey("Microsoft.NET.Test.Sdk"));
        Assert.IsFalse(packageReferences.ContainsKey("Reqnroll.MsTest"));

    }

#if false
    /// <summary>
    /// Ensures application settings and Reqnroll content semantics stay project-type aware and independently disableable.
    /// </summary>
    [TestMethod]
    public async Task SdkContentAndReqnrollProfileAppliesOnlyToDetectedTestProjects()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-content-reqnroll");
        var feed = _prepareSdkFixture(fixtureRoot);

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
        Assert.AreEqual("Always", testConfig!["CopyToOutputDirectory"]);
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
#endif

#if false
    /// <summary>
    /// Ensures exact analyzer references, opt-outs, SQL exclusion, and package boundaries compose with Build.
    /// </summary>
    [TestMethod]
    public async Task SdkAnalyzerReferencesAreExactSwitchableAndCapabilitySafe()
    {
        const string packageVersion = _packageVersion;
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-analyzer-references");
        var feed = _prepareSdkFixture(fixtureRoot);

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
            ["EnableArkToolsBannedApi"] = ("Microsoft.CodeAnalysis.BannedApiAnalyzers", "AdditionalFiles", "BannedSymbols.Ark.txt"),
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
            Assert.IsTrue(fsharpPackages.ContainsKey(analyzer), analyzer);
        }
        Assert.IsTrue(File.Exists(Path.Join(fixtureRoot, "fsharp", "packages.lock.json")));
    }
#endif

    /// <summary>
    /// Ensures generated lock files, locked CI restore, and CPM ownership boundaries are enforced.
    /// </summary>
    [TestMethod]
    public async Task SdkLockFileAndCpmBoundariesAreEnforced()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-lock-and-cpm");
        var feed = _prepareSdkFixture(fixtureRoot);

        var lockedRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "locked",
            "Consumer.csproj",
            _createSdkCSharpProject());
        var lockedEnvironment = _createSdkEnvironment(fixtureRoot);
        await _run(
            "dotnet",
            $"msbuild \"{Path.Join(lockedRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(lockedRoot, "NuGet.Config")}\"",
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
            Assert.IsFalse(dependencies.TryGetProperty("Microsoft.VisualStudio.Threading.Analyzers", out _));
        }

        var threadingOptInRoot = await _createSdkScenarioAsync(
            fixtureRoot,
            feed,
            "threading-opt-in",
            "Consumer.csproj",
            _createSdkCSharpProject(),
            directoryProperties: "<EnableArkToolsVisualStudioThreading>true</EnableArkToolsVisualStudioThreading>");
        await _run(
            "dotnet",
            $"msbuild \"{Path.Join(threadingOptInRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(threadingOptInRoot, "NuGet.Config")}\"",
            _createSdkEnvironment(fixtureRoot));
        using (var optInLockJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(threadingOptInRoot, "packages.lock.json")).ConfigureAwait(false)))
        {
            var dependencies = optInLockJson.RootElement.GetProperty("dependencies").GetProperty("net10.0");
            Assert.AreEqual(
                _visualStudioThreadingAnalyzerVersion,
                dependencies.GetProperty("Microsoft.VisualStudio.Threading.Analyzers").GetProperty("resolved").GetString());
        }

        lockedEnvironment["CI"] = "true";
        Directory.Delete(Path.Join(lockedRoot, "obj"), true);
        await _run(
            "dotnet",
            $"msbuild \"{Path.Join(lockedRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);

        await File.WriteAllTextAsync(
            Path.Join(lockedRoot, "Consumer.csproj"),
            _createSdkCSharpProject(targetFramework: "net8.0")).ConfigureAwait(false);
        Directory.Delete(Path.Join(lockedRoot, "obj"), true);
        var lockedFailure = await _runForExitCode(
            "dotnet",
            $"msbuild \"{Path.Join(lockedRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(lockedRoot, "NuGet.Config")}\"",
            lockedEnvironment);
        Assert.AreNotEqual(0, lockedFailure.ExitCode);
        StringAssert.Contains(lockedFailure.Output, "NU1004", StringComparison.Ordinal);

        await File.WriteAllTextAsync(
            Path.Join(lockedRoot, "Consumer.csproj"),
            _createSdkCSharpProject("<RestoreLockedMode>false</RestoreLockedMode>", targetFramework: "net8.0")).ConfigureAwait(false);
        await _run(
            "dotnet",
            $"msbuild \"{Path.Join(lockedRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(lockedRoot, "NuGet.Config")}\"",
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
            $"msbuild \"{Path.Join(cpmRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(cpmRoot, "NuGet.Config")}\"",
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
            $"msbuild \"{Path.Join(cpmRoot, "Consumer.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(cpmRoot, "NuGet.Config")}\"",
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
        const string packageVersion = _packageVersion;
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-sponsor-link");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = _feed;

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
    /// Ensures a basic SDK consumer builds and runs through the configured test host.
    /// </summary>
    [TestMethod]
    public async Task SdkConsumerRunsWithDotnetTest()
    {
        var fixtureRoot = Path.Join(_root, "artifacts", "sdk-dotnet-test");
        _prepareSdkFixture(fixtureRoot);
        var scenarioRoot = fixtureRoot;
        await _createSdkScenarioAsync(
            fixtureRoot,
            _feed,
            "consumer-tests",
            "Consumer.Tests.csproj",
            _createSdkTestProject());
        await File.WriteAllTextAsync(
            Path.Join(scenarioRoot, "consumer-tests", "ConsumerTests.cs"),
            """
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Verifies the generated consumer test project.
/// </summary>
[TestClass]
public sealed class ConsumerTests
{
    /// <summary>
    /// Verifies basic test execution.
    /// </summary>
    [TestMethod]
    public void BasicPropertiesSupportTestExecution()
    {
        Assert.AreEqual(2, 1 + 1);
    }
}
""").ConfigureAwait(false);
        var testRoot = Path.Join(scenarioRoot, "consumer-tests");
        await _run(
            "dotnet",
            $"msbuild \"{Path.Join(testRoot, "Consumer.Tests.csproj")}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(testRoot, "NuGet.Config")}\"",
            _createSdkEnvironment(scenarioRoot));
        await _run(
            "dotnet",
            $"test \"{Path.Join(testRoot, "Consumer.Tests.csproj")}\" --no-restore",
            _createSdkEnvironment(scenarioRoot));
    }

#if false
    /// <summary>
    /// Ensures packed Build assets select only the accepted capability-specific policy in clean consumers.
    /// </summary>
    [TestMethod]
    public async Task BuildBaselineIsCapabilitySafeAndOverridable()
    {
        const string packageVersion = _packageVersion;
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var fixtureRoot = Path.Join(root, "artifacts", "sdk-build-baseline");
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        var feed = _feed;

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
#endif

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

    private static string _createSdkTestProject()
    {
        return """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.3.3" />
    <PackageReference Include="MSTest.TestFramework" Version="4.3.3" />
    <PackageReference Include="MSTest.Analyzers" Version="4.3.3" />
  </ItemGroup>
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

    private static string _prepareSdkFixture(string fixtureRoot)
    {
        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, true);
        }
        Directory.CreateDirectory(fixtureRoot);
        return _feed;
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
            .Select(static name => name?["Ark.Tools.Sdk.".Length..] ?? "")
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
            $"msbuild \"{projectPath}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(scenarioRoot, "NuGet.Config")}\" -p:RestoreLockedMode=false",
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
            ["NUGET_PACKAGES"] = _packageCache,
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
                static item => item.GetProperty("Identity").GetString() ?? "",
                static item => item.EnumerateObject().ToDictionary(
                    static property => property.Name,
                    static property => property.Value.GetString() ?? ""),
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
            $"msbuild \"{projectPath}\" -target:Restore -p:RestoreConfigFile=\"{Path.Join(scenarioRoot, "NuGet.Config")}\"{globalProperty}",
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
            .Select(static identity => Path.GetFileName(identity) ?? "")
            .Where(_allSyntheticAnalyzers.Contains)
            .ToArray();
    }

    private static Dictionary<string, string> _createEnvironment(string scenarioRoot)
    {
        return new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = _packageCache,
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(scenarioRoot, "http-cache")
        };
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
            .Where(static identity => identity.Contains("ark.tools.build", StringComparison.OrdinalIgnoreCase))
            .Select(static identity => Path.GetFileName(identity) ?? "")
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
            .Select(static item => item.GetProperty("Identity").GetString() ?? "")
            .ToArray();
    }

    private static string[] _getSdkItemIdentities(JsonDocument evaluation, string itemName)
    {
        return evaluation.RootElement.GetProperty("Items").GetProperty(itemName)
            .EnumerateArray()
            .Where(static item => item.TryGetProperty("DefiningProjectName", out var definingProjectName) &&
                string.Equals(definingProjectName.GetString(), "Sdk", StringComparison.Ordinal))
            .Select(static item => item.GetProperty("Identity").GetString() ?? "")
            .ToArray();
    }

    private static Dictionary<string, string>? _findItem(JsonDocument evaluation, string itemName, string fileName)
    {
        var matches = evaluation.RootElement.GetProperty("Items").GetProperty(itemName)
            .EnumerateArray()
            .Select(static item =>
            {
                var identity = item.GetProperty("Identity").GetString() ?? "";
                var metadata = item.EnumerateObject()
                    .Where(static property => !string.Equals(property.Name, "Identity", StringComparison.Ordinal))
                    .ToDictionary(
                        static property => property.Name,
                        static property => property.Value.GetString() ?? "",
                        StringComparer.Ordinal);
                return new
                {
                    Identity = identity,
                    DefiningProjectName = metadata.TryGetValue("DefiningProjectName", out var definingProjectName) ? definingProjectName : "",
                    Metadata = metadata
                };
            })
            .Where(item => string.Equals(Path.GetFileName(item.Identity), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static item => string.Equals(item.DefiningProjectName, "Sdk", StringComparison.Ordinal))
            .ThenByDescending(static item => item.Metadata.ContainsKey("CopyToOutputDirectory"))
            .ThenByDescending(static item => item.Metadata.ContainsKey("CopyToPublishDirectory"))
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
