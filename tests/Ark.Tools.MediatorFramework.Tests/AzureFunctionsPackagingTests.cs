// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.IO.Compression;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>
/// Proves the Ark.Tools.MediatorFramework.AzureFunctions NuGet package ships its source
/// generator under analyzers/dotnet/cs and no unintended implementation assets.
/// </summary>
[TestClass]
public sealed class AzureFunctionsPackagingTests
{
    /// <summary>Packs the runtime package and verifies the analyzer asset and package shape.</summary>
    [TestMethod]
    public async Task PackageContainsGeneratorUnderAnalyzersDotnetCs()
    {
        var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var feed = Path.Join(root, "artifacts", "azurefunctions-pack-test");
        Directory.CreateDirectory(feed);
        var project = Path.Join(root, "src", "mediator-framework", "Ark.Tools.MediatorFramework.AzureFunctions", "Ark.Tools.MediatorFramework.AzureFunctions.csproj");
        await _run("dotnet", $"pack \"{project}\" -c Debug -o \"{feed}\" -p:PackageVersion=999.9.9");

        await using var package = await ZipFile.OpenReadAsync(Path.Join(feed, "Ark.Tools.MediatorFramework.AzureFunctions.999.9.9.nupkg"));
        package.GetEntry("analyzers/dotnet/cs/Ark.Tools.MediatorFramework.AzureFunctions.Generators.dll").Should().NotBeNull();
        package.GetEntry("lib/net10.0/Ark.Tools.MediatorFramework.AzureFunctions.dll").Should().NotBeNull();
        package.GetEntry("buildTransitive/Ark.Tools.MediatorFramework.AzureFunctions.props").Should().NotBeNull();
        package.Entries.Should().NotContain(static e => e.FullName.StartsWith("lib/", StringComparison.Ordinal) && e.FullName.Contains("Generators", StringComparison.Ordinal),
            "the generator must not ship as an implementation assembly");
        package.Entries.Where(static e => e.FullName.StartsWith("lib/net10.0/", StringComparison.Ordinal) && e.FullName.EndsWith(".dll", StringComparison.Ordinal))
            .Should().ContainSingle("only the package's own assembly ships under lib");
    }

    private static async Task<string> _run(string fileName, string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false) + await errorTask.ConfigureAwait(false);
        Assert.AreEqual(0, process.ExitCode, output);
        return output;
    }
}
