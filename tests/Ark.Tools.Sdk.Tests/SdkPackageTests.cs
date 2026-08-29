// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Compression;

namespace Ark.Tools.Sdk.Tests;

/// <summary>
/// Verifies the foundation package archives and their clean-consumer assets.
/// </summary>
[TestClass]
public sealed class SdkPackageTests
{
    /// <summary>
    /// Ensures both package archives contain only their intended MSBuild assets.
    /// </summary>
    [TestMethod]
    public void PackageArchivesContainExpectedAssets()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var feed = Path.Combine(root, "artifacts", "sdk-test-feed");
        Directory.CreateDirectory(feed);
        Run("dotnet", $"pack \"{Path.Combine(root, "src/sdk/Ark.Tools.Build/Ark.Tools.Build.csproj")}\" -c Debug -o \"{feed}\" --no-restore -p:PackageVersion=999.9.9");
        Run("dotnet", $"pack \"{Path.Combine(root, "src/sdk/Ark.Tools.Sdk/Ark.Tools.Sdk.csproj")}\" -c Debug -o \"{feed}\" --no-restore -p:PackageVersion=999.9.9");

        using var build = ZipFile.OpenRead(Path.Combine(feed, "Ark.Tools.Build.999.9.9.nupkg"));
        using var sdk = ZipFile.OpenRead(Path.Combine(feed, "Ark.Tools.Sdk.999.9.9.nupkg"));
        Assert.IsNotNull(build.GetEntry("build/Ark.Tools.Build.props"));
        Assert.IsNotNull(build.GetEntry("buildTransitive/Ark.Tools.Build.props"));
        Assert.IsNull(build.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)));
        var sdkProps = new StreamReader(sdk.GetEntry("Sdk/Sdk.props")!.Open()).ReadToEnd();
        StringAssert.Contains(sdkProps, "Version=\"999.9.9\"");
        StringAssert.Contains(sdkProps, "IsImplicitlyDefined=\"true\"");
        Assert.IsNull(sdk.Entries.FirstOrDefault(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)));
        using (var nuspec = new StreamReader(build.GetEntry("Ark.Tools.Build.nuspec")!.Open()))
        {
            Assert.IsFalse(nuspec.ReadToEnd().Contains("<dependencies>", StringComparison.Ordinal));
        }

        var consumer = Path.Combine(root, "artifacts", "sdk-consumer");
        Directory.CreateDirectory(consumer);
        File.WriteAllText(Path.Combine(consumer, "Directory.Build.props"), "<Project><PropertyGroup><ArkToolsPackageProject>true</ArkToolsPackageProject><RestorePackagesWithLockFile>false</RestorePackagesWithLockFile><EnablePackageValidation>false</EnablePackageValidation></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(consumer, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(consumer, "global.json"), """{"sdk":{"version":"10.0.400"},"msbuild-sdks":{"Ark.Tools.Sdk":"999.9.9"}}""");
        File.WriteAllText(Path.Combine(consumer, "NuGet.Config"), $"<configuration><packageSources><clear /><add key=\"local\" value=\"{feed}\" /></packageSources></configuration>");
        File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
""");
        var packages = Path.Combine(consumer, "packages");
        File.Delete(Path.Combine(consumer, "packages.lock.json"));
        var environment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = packages,
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(consumer, "http-cache")
        };
        Run("dotnet", $"restore \"{Path.Combine(consumer, "Consumer.csproj")}\" --configfile \"{Path.Combine(consumer, "NuGet.Config")}\"", environment);
        var properties = Run("dotnet", $"msbuild \"{Path.Combine(consumer, "Consumer.csproj")}\" -getProperty:ArkToolsBuildImported -getProperty:ArkToolsBuildImportCount", environment);
        StringAssert.Contains(properties, "\"ArkToolsBuildImported\": \"true\"");
        StringAssert.Contains(properties, "\"ArkToolsBuildImportCount\": \"1\"");

        var disabled = Path.Combine(root, "artifacts", "sdk-consumer-disabled");
        Directory.CreateDirectory(disabled);
        File.Copy(Path.Combine(consumer, "global.json"), Path.Combine(disabled, "global.json"), true);
        File.Copy(Path.Combine(consumer, "NuGet.Config"), Path.Combine(disabled, "NuGet.Config"), true);
        File.Copy(Path.Combine(consumer, "Directory.Build.props"), Path.Combine(disabled, "Directory.Build.props"), true);
        File.Delete(Path.Combine(disabled, "packages.lock.json"));
        File.WriteAllText(Path.Combine(disabled, "Consumer.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableArkToolsBuild>false</EnableArkToolsBuild>
  </PropertyGroup>
</Project>
""");
        var disabledEnvironment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Combine(disabled, "packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(disabled, "http-cache")
        };
        Run("dotnet", $"restore \"{Path.Combine(disabled, "Consumer.csproj")}\" --configfile \"{Path.Combine(disabled, "NuGet.Config")}\"", disabledEnvironment);
        var disabledProperties = Run("dotnet", $"msbuild \"{Path.Combine(disabled, "Consumer.csproj")}\" -getProperty:ArkToolsBuildImported", disabledEnvironment);
        Assert.IsFalse(disabledProperties.Contains("\"ArkToolsBuildImported\": \"true\"", StringComparison.Ordinal));
    }

    private static string Run(string fileName, string arguments, IDictionary<string, string>? environment = null)
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
        process.WaitForExit();
        var error = process.StandardError.ReadToEnd();
        var output = process.StandardOutput.ReadToEnd();
        Assert.AreEqual(0, process.ExitCode, $"{output}{Environment.NewLine}{error}");
        return output;
    }
}
