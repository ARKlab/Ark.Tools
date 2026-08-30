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
        "RunSqlCodeAnalysis"
    ];

    private static readonly string[] _canonicalTargetProperties =
    [
        "ArkToolsBuildImported",
        "ArkToolsBuildImportCount"
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
    /// Ensures the canonical Build assets contain only the accepted public property baseline.
    /// </summary>
    [TestMethod]
    public void CanonicalBuildAssetsContainOnlyAcceptedProperties()
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
        Assert.IsFalse(props.Descendants("ItemGroup").Any());
        Assert.IsFalse(targets.Descendants("ItemGroup").Any());
        Assert.IsFalse(targets.Descendants("Target").Any());
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

    private static string _createCSharpProject(string? packageVersion, string properties = "")
    {
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    {properties}
  </PropertyGroup>
  {_createPackageReference(packageVersion)}
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
        var environment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Join(scenarioRoot, "packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Join(scenarioRoot, "http-cache")
        };
        var globalProperty = globalDisable ? " -p:EnableArkToolsBuild=false" : "";
        await _run(
            "dotnet",
            $"restore \"{projectPath}\" --configfile \"{Path.Join(scenarioRoot, "NuGet.Config")}\"{globalProperty}",
            environment);
        var propertyNames = _selectedProperties.Concat(_boundaryProperties).Append("ArkToolsBuildImported").Append("UsingMicrosoftBuildSqlSdk");
        var output = await _run(
            "dotnet",
            $"msbuild \"{projectPath}\" -getProperty:{string.Join(",", propertyNames)} -getItem:{string.Join(",", _boundaryItems.Append("Using"))}{globalProperty}",
            environment);
        return JsonDocument.Parse(output);
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
                .Where(identity => !string.Equals(identity, ignoredIdentity, StringComparison.Ordinal))
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
        Assert.AreEqual(0, process.ExitCode, $"{output}{Environment.NewLine}{error}");
        return output;
    }
}
